using Corvette.MeteoExport.Contracts;
using MassTransit;

namespace Corvette.MeteoExport.Worker.Consumers;

/// <summary>
/// Точка приёма сообщений о сбое обработки выгрузки.
/// </summary>
public class ExportFaultConsumerDefinition : ConsumerDefinition<ExportFaultConsumer>
{
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    public ExportFaultConsumerDefinition()
    {
        EndpointName = EndpointNames.ExportsFault;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ExportFaultConsumer> consumerConfigurator,
        IRegistrationContext registrationContext)
    {
        endpointConfigurator.UseMessageRetry(retry => retry.Interval(RetryCount, RetryInterval));

        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbit)
        {
            rabbit.SetQuorumQueue();
        }
    }
}
