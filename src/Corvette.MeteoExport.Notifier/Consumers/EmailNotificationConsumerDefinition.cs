using Corvette.MeteoExport.Contracts;
using MassTransit;

namespace Corvette.MeteoExport.Notifier.Consumers;

/// <summary>
/// Точка приёма писем.
/// </summary>
public class EmailNotificationConsumerDefinition : ConsumerDefinition<EmailNotificationConsumer>
{
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    public EmailNotificationConsumerDefinition()
    {
        EndpointName = EndpointNames.NotifyEmail;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<EmailNotificationConsumer> consumerConfigurator,
        IRegistrationContext registrationContext)
    {
        endpointConfigurator.UseMessageRetry(retry =>
        {
            // Негодное событие не станет годным при повторе.
            retry.Ignore<ArgumentException>();
            retry.Interval(RetryCount, RetryInterval);
        });

        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbit)
        {
            rabbit.SetQuorumQueue();
        }
    }
}
