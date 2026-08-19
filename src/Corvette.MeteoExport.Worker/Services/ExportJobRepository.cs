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
    private readonly MeteoExportDbContext _context;
    private readonly ILogger<ExportJobRepository> _logger;

    public ExportJobRepository(MeteoExportDbContext context, ILogger<ExportJobRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Берёт задание в работу
    /// </summary>
    /// <remarks>
    /// Открывает свою транзакцию
    /// </remarks>
    /// <returns>
    /// null - если брать нечего
    /// </returns>
    public async Task<ExportJobEntity?> ClaimAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            // берём строку с блокировкой
            var job = await _context.ExportJobs
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

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return job;
        }
    }

    /// <summary>
    /// Запоминает, сколько кусков работы предстоит и сколько из них уже сделано.
    /// </summary>
    public async Task SaveProgressAsync(Guid jobId, int chunksDone, int chunksTotal, CancellationToken cancellationToken)
    {
        if (chunksDone < 0) throw new ArgumentOutOfRangeException(nameof(chunksDone), "Число выполненных кусков отрицательное.");
        if (chunksTotal <= 0) throw new ArgumentOutOfRangeException(nameof(chunksTotal), "Число кусков работы должно быть больше нуля.");

        // Условие на Running: перехваченное соседом задание уже не наше, о потере скажет завершение.
        await _context.ExportJobs
            .Where(x => x.Id == jobId && x.Status == ExportStatus.Running)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ChunksDone, chunksDone)
                .SetProperty(x => x.ChunksTotal, chunksTotal), cancellationToken);
    }

    /// <summary>
    /// Ставит заданию конечный статус.
    /// </summary>
    /// <remarks>
    /// Работает в чужой транзакции
    /// </remarks>
    /// <returns>
    /// Задание с проставленным результатом
    /// null - если оно уже не в работе
    /// </returns>
    public async Task<ExportJobEntity?> FinishAsync(
        Guid jobId,
        ExportStatus status,
        string? resultFilePath,
        string? error,
        CancellationToken cancellationToken)
    {
        if (status != ExportStatus.Completed && status != ExportStatus.Failed) throw new ArgumentOutOfRangeException(nameof(status), "Итогом может быть только конечный статус.");

        var job = await _context.ExportJobs.SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job == null)
        {
            _logger.LogError($"Задания нет в базе, итог подводить нечему (JobId=\"{jobId}\")");
            return null;
        }

        if (job.Status != ExportStatus.Running)
        {
            _logger.LogWarning($"Задание уже не в работе, статус не изменён (JobId=\"{jobId}\", Status=\"{job.Status}\")");
            return null;
        }

        job.Status = status;
        job.FinishedAt = DateTime.UtcNow;
        job.Error = error;
        job.ResultFilePath = resultFilePath;

        await _context.SaveChangesAsync(cancellationToken);

        return job;
    }
}
