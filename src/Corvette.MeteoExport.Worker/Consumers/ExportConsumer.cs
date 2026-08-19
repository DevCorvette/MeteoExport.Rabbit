using Corvette.MeteoExport.Contracts;
using Corvette.MeteoExport.Worker.Services;
using MassTransit;

namespace Corvette.MeteoExport.Worker.Consumers;

/// <summary>
/// Выполняет команду на выгрузку.
/// </summary>
public class ExportConsumer : IConsumer<RunExportMessage>
{
    private readonly ExportRunner _runner;

    public ExportConsumer(ExportRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task Consume(ConsumeContext<RunExportMessage> consumeContext)
    {
        ArgumentNullException.ThrowIfNull(consumeContext);

        var jobId = consumeContext.Message.JobId;
        if (jobId == Guid.Empty) throw new ArgumentException("В команде нет идентификатора задания.", nameof(consumeContext));

        await _runner.RunAsync(jobId, consumeContext.CancellationToken);
    }
}
