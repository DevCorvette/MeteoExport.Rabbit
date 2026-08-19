using Corvette.MeteoExport.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Consumers;

/// <summary>
/// Ставит точку в задании, у которого кончились повторы.
/// </summary>
/// <remarks>
/// Шина публикует это сообщение, когда команда исчерпала повторы и уехала в очередь ошибок.
/// </remarks>
public class ExportFaultConsumer : IConsumer<Fault<RunExportMessage>>
{
    private const string UnknownReason = "Причина неизвестна.";

    private readonly ILogger<ExportFaultConsumer> _logger;

    public ExportFaultConsumer(ILogger<ExportFaultConsumer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<Fault<RunExportMessage>> consumeContext)
    {
        ArgumentNullException.ThrowIfNull(consumeContext);

        var jobId = consumeContext.Message.Message.JobId;
        var error = consumeContext.Message.Exceptions.FirstOrDefault()?.Message ?? UnknownReason;

        _logger.LogError($"Повторы исчерпаны, задание провалено (JobId=\"{jobId}\", Error=\"{error}\")");

        var endpoint = await consumeContext.GetSendEndpoint(new Uri($"exchange:{EndpointNames.ExportsFinish}"));
        await endpoint.Send(new FinishExportMessage { JobId = jobId, Error = error }, consumeContext.CancellationToken);
    }
}
