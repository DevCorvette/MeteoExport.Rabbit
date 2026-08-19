using Corvette.MeteoExport.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Services;

/// <summary>
/// Выполняет задание на выгрузку - оркестр: расставляет шаги и сдаёт работу финализатору.
/// </summary>
public class ExportRunner
{
    private readonly ExportJobRepository _repository;
    private readonly ExportBuilder _builder;
    private readonly ResultStorage _storage;
    private readonly DraftFiles _drafts;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<ExportRunner> _logger;

    public ExportRunner(
        ExportJobRepository repository,
        ExportBuilder builder,
        ResultStorage storage,
        DraftFiles drafts,
        ISendEndpointProvider sendEndpointProvider,
        ILogger<ExportRunner> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
        _sendEndpointProvider = sendEndpointProvider ?? throw new ArgumentNullException(nameof(sendEndpointProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Выполняет задание по команде и сдаёт результат финализатору.
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

        // собираем файл
        await _builder.BuildAsync(job, draftPath, cancellationToken);

        _logger.LogInformation($"Выгрузка завершена (JobId=\"{job.Id}\", DraftPath=\"{draftPath}\")");

        // отдаём в хранилище
        var resultKey = await _storage.UploadAsync(draftPath, job.Id, cancellationToken);
        _drafts.Delete(job.Id);

        // сдаём работу
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri($"exchange:{EndpointNames.ExportsFinish}"));
        await endpoint.Send(new FinishExportMessage { JobId = job.Id, ResultFilePath = resultKey }, cancellationToken);

        _logger.LogInformation($"Выгрузка сдана финализатору (JobId=\"{job.Id}\", ResultFilePath=\"{resultKey}\")");
    }
}
