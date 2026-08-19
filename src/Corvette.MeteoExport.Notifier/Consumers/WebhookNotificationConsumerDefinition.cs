using Corvette.MeteoExport.Contracts;
using MassTransit;

namespace Corvette.MeteoExport.Notifier.Consumers;

/// <summary>
/// Точка приёма вебхуков.
/// </summary>
public class WebhookNotificationConsumerDefinition : ConsumerDefinition<WebhookNotificationConsumer>
{
    private const int RetryCount = 5;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    public WebhookNotificationConsumerDefinition()
    {
        EndpointName = EndpointNames.NotifyWebhook;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<WebhookNotificationConsumer> consumerConfigurator,
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
