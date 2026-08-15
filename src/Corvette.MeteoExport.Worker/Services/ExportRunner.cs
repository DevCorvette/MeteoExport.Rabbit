using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Services;

/// <summary>
/// Выполняет задание на выгрузку: расставляет шаги и следит за статусом.
/// </summary>
public class ExportRunner
{
    private readonly ExportJobRepository _repository;
    private readonly ExportBuilder _builder;
    private readonly ResultStorage _storage;
    private readonly DraftFiles _drafts;
    private readonly ILogger<ExportRunner> _logger;

    public ExportRunner(
        ExportJobRepository repository,
        ExportBuilder builder,
        ResultStorage storage,
        DraftFiles drafts,
        ILogger<ExportRunner> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Выполняет задание по команде и доводит его до конечного статуса.
    /// </summary>
    public async Task RunAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Начинаем обработку команды (JobId=\"{jobId}\")");

        var job = await _repository.ClaimAsync(jobId, cancellationToken);

        // Задания нет или оно уже завершено
        if (job == null)
            return;

        _logger.LogInformation($"Задание получено (JobId=\"{job.Id}\", Points={job.Points.Count}, From=\"{job.FromDate:O}\", To=\"{job.ToDate:O}\", Variables={job.Variables.Length})");

        var draftPath = _drafts.GetPath(job.Id);

        try
        {
            // собираем файл
            await _builder.BuildAsync(job, draftPath, cancellationToken);

            _logger.LogInformation($"Выгрузка завершена (JobId=\"{job.Id}\", DraftPath=\"{draftPath}\")");

            // отдаём в хранилище
            var resultKey = await _storage.UploadAsync(draftPath, job.Id, cancellationToken);
            _drafts.Delete(job.Id);

            // завершаем
            await _repository.CompleteAsync(job.Id, resultKey, cancellationToken);

            _logger.LogInformation($"Задание завершено (JobId=\"{job.Id}\", ResultFilePath=\"{resultKey}\")");
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
}
