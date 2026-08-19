using Corvette.MeteoExport.Core.Messages;
using Corvette.MeteoExport.Core.Models;
using Corvette.MeteoExport.Worker.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Consumers;

/// <summary>
/// Подводит итог выгрузки: ставит конечный статус и публикует событие о завершении.
/// </summary>
/// <remarks>
/// Оба действия уезжают одной транзакцией
/// </remarks>
public class FinishExportConsumer : IConsumer<FinishExportMessage>
{
    private readonly ExportJobRepository _repository;
    private readonly ILogger<FinishExportConsumer> _logger;

    public FinishExportConsumer(ExportJobRepository repository, ILogger<FinishExportConsumer> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<FinishExportMessage> consumeContext)
    {
        ArgumentNullException.ThrowIfNull(consumeContext);

        var message = consumeContext.Message;
        if (message.JobId == Guid.Empty) throw new ArgumentException("В команде нет идентификатора задания.", nameof(consumeContext));

        var status = message.Error == null ? ExportStatus.Completed : ExportStatus.Failed;

        var job = await _repository.FinishAsync(
            message.JobId,
            status,
            message.ResultFilePath,
            message.Error,
            consumeContext.CancellationToken);

        // Итог уже подведён — второй раз не публикуем
        if (job == null)
            return;

        await consumeContext.Publish(new ExportFinishedMessage
        {
            JobId = job.Id,
            Status = job.Status,
            FinishedAt = job.FinishedAt!.Value,
            Email = job.Email,
            WebhookUrl = job.WebhookUrl,
            Error = job.Error,
        }, consumeContext.CancellationToken);

        _logger.LogInformation($"Итог подведён (JobId=\"{job.Id}\", Status=\"{job.Status}\", ResultFilePath=\"{job.ResultFilePath}\")");
    }
}
