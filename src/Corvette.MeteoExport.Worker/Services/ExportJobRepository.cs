using Corvette.MeteoExport.Core;
using Corvette.MeteoExport.Core.Entities;
using Corvette.MeteoExport.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Services;

/// <summary>
/// Переводит задание по статусам.
/// </summary>
public class ExportJobRepository
{
    private readonly IDbContextFactory<MeteoExportDbContext> _contextFactory;
    private readonly ILogger<ExportJobRepository> _logger;

    public ExportJobRepository(IDbContextFactory<MeteoExportDbContext> contextFactory, ILogger<ExportJobRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Берёт задание в работу
    /// </summary>
    /// <returns>
    /// null - если брать нечего
    /// </returns>
    public async Task<ExportJobEntity?> ClaimAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            // берём строку с блокировкой
            var job = await context.ExportJobs
                .FromSql($"select * from export_jobs where id = {jobId} for update")
                .SingleOrDefaultAsync(cancellationToken);

            if (job == null)
            {
                _logger.LogError($"Задания нет в базе, команда пропущена (JobId=\"{jobId}\")");
                return null;
            }

            if (job.Status is not (ExportStatus.Queued or ExportStatus.Running))
            {
                _logger.LogWarning($"Задание уже завершено, команда пропущена (JobId=\"{jobId}\", Status=\"{job.Status}\")");
                return null;
            }

            if (job.Status == ExportStatus.Running)
            {
                // Прежнего владельца брокер списал: либо тот умер, либо отрезан от очереди
                _logger.LogWarning($"Задание перехвачено у другого воркера, выгрузка начнётся заново (JobId=\"{jobId}\")");
            }
            else
            {
                _logger.LogInformation($"Задание взято в работу (JobId=\"{jobId}\")");
            }

            // захват
            job.Status = ExportStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.ChunksDone = 0; // Выгрузка начинается с нуля, прогресс прошлой попытки к ней отношения не имеет
            job.Error = null;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return job;
        }
    }

    /// <summary>
    /// Отмечает задание выполненным и запоминает, где лежит файл.
    /// </summary>
    public async Task CompleteAsync(Guid jobId, string resultFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resultFilePath)) throw new ArgumentException("Не задан путь к файлу результата.", nameof(resultFilePath));

        await FinishAsync(jobId, ExportStatus.Completed, error: null, resultFilePath, cancellationToken);
    }

    /// <summary>
    /// Отмечает задание неудавшимся.
    /// </summary>
    public async Task FailAsync(Guid jobId, string error, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(error)) throw new ArgumentException("Не задан текст ошибки.", nameof(error));

        await FinishAsync(jobId, ExportStatus.Failed, error, resultFilePath: null, cancellationToken);
    }

    private async Task FinishAsync(Guid jobId, ExportStatus status, string? error, string? resultFilePath, CancellationToken cancellationToken)
    {
        await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            // Условие на Running: задание, перехваченное соседним воркером, уже не наше.
            var finished = await context.ExportJobs
                .Where(x => x.Id == jobId && x.Status == ExportStatus.Running)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.FinishedAt, DateTime.UtcNow)
                    .SetProperty(x => x.Error, error)
                    .SetProperty(x => x.ResultFilePath, resultFilePath), cancellationToken);

            if (finished == 0)
                _logger.LogWarning($"Задание уже не в работе, статус не изменён (JobId=\"{jobId}\", Status=\"{status}\")");
        }
    }
}
