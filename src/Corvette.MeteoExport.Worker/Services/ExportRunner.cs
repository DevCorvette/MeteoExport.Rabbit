using System.Globalization;
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
    private readonly ResultStorage _storage;
    private readonly DraftFiles _drafts;
    private readonly ILogger<ExportRunner> _logger;

    public ExportRunner(ExportJobRepository repository, ResultStorage storage, DraftFiles drafts, ILogger<ExportRunner> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
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
            var draftPath = _drafts.GetPath(job.Id);
            
            // собираем файл
            await ExportAsync(job, draftPath, cancellationToken);

            // отдаём в хранилище
            var resultKey = await _storage.UploadAsync(draftPath, job.Id, cancellationToken);
            _drafts.Delete(job.Id);

            // завершаем
            await _repository.CompleteAsync(job.Id, resultKey, cancellationToken);

            _logger.LogInformation($"Задание выполнено (JobId=\"{job.Id}\", ResultFilePath=\"{resultKey}\")");
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

    private async Task ExportAsync(ExportJobEntity job, string draftPath, CancellationToken cancellationToken)
    {
        // заглушка
        var lines = new List<string> { string.Join(',', new[] { "latitude", "longitude", "name", "date" }.Concat(job.Variables)) };

        foreach (var point in job.Points)
        {
            var values = new[] { point.Latitude.ToString(CultureInfo.InvariantCulture), point.Longitude.ToString(CultureInfo.InvariantCulture), point.Name, job.FromDate.ToString("O") };
            lines.Add(string.Join(',', values.Concat(job.Variables.Select(_ => string.Empty))));
        }

        await File.WriteAllLinesAsync(draftPath, lines, cancellationToken);

        await Task.Delay(StubDuration, cancellationToken);
    }
}
