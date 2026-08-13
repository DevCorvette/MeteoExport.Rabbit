using Corvette.MeteoExport.Core.Entities;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Services;

/// <summary>
/// Выполняет задание на выгрузку.
/// </summary>
public class ExportRunner
{
    /// <summary>
    /// Сколько заглушка изображает работу.
    /// </summary>
    private static readonly TimeSpan StubDuration = TimeSpan.FromSeconds(5);

    private readonly ExportJobRepository _repository;
    private readonly ILogger<ExportRunner> _logger;

    public ExportRunner(ExportJobRepository repository, ILogger<ExportRunner> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Выполняет задание по команде и доводит его до конечного статуса.
    /// </summary>
    public async Task RunAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _repository.ClaimAsync(jobId, cancellationToken);

        // Задания нет или оно уже завершено
        if (job == null)
            return;

        try
        {
            await ExportAsync(job, cancellationToken);

            await _repository.CompleteAsync(job.Id, cancellationToken);

            _logger.LogInformation($"Задание выполнено (JobId=\"{job.Id}\")");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError($"Задание не выполнено (JobId=\"{job.Id}\", Error=\"{exception.Message}\")");

            // Наше дело — записать статус, разбирается с ошибкой шина.
            await _repository.FailAsync(job.Id, exception.Message, cancellationToken);
            throw;
        }
    }

    private async Task ExportAsync(ExportJobEntity job, CancellationToken cancellationToken)
    {
        // заглушка
        await Task.Delay(StubDuration, cancellationToken);
    }
}
