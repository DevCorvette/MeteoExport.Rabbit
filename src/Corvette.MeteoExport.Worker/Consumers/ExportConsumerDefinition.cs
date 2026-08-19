using Corvette.MeteoExport.Contracts;
using MassTransit;

namespace Corvette.MeteoExport.Worker.Consumers;

/// <summary>
/// Как выглядит очередь потребителя и что делать с упавшими командами.
/// </summary>
public class ExportConsumerDefinition : ConsumerDefinition<ExportConsumer>
{
    /// <summary>
    /// Сколько раз шина повторит команду, прежде чем отправить её в очередь ошибок.
    /// </summary>
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    public ExportConsumerDefinition()
    {
        EndpointName = EndpointNames.Exports;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ExportConsumer> consumerConfigurator,
        IRegistrationContext registrationContext)
    {
        // Задание занимает воркер целиком.
        endpointConfigurator.PrefetchCount = 1;
        endpointConfigurator.ConcurrentMessageLimit = 1;

        endpointConfigurator.UseMessageRetry(retry =>
        {
            // Негодная команда не станет годной при повторе.
            retry.Ignore<ArgumentException>();
            retry.Interval(RetryCount, RetryInterval);
        });

        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbit)
        {
            rabbit.SetQuorumQueue();
        }
    }
}
