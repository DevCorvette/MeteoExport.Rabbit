using Corvette.MeteoExport.Core;
using Corvette.MeteoExport.Core.Messages;
using MassTransit;

namespace Corvette.MeteoExport.Worker.Consumers;

/// <summary>
/// Точка приёма финализатора: короткая работа под инбоксом.
/// </summary>
public class FinishExportConsumerDefinition : ConsumerDefinition<FinishExportConsumer>
{
    private const int RetryCount = 3;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    public FinishExportConsumerDefinition()
    {
        EndpointName = EndpointNames.ExportsFinish;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<FinishExportConsumer> consumerConfigurator,
        IRegistrationContext registrationContext)
    {
        endpointConfigurator.UseMessageRetry(retry =>
        {
            // Негодная команда не станет годной при повторе.
            retry.Ignore<ArgumentException>();
            retry.Interval(RetryCount, RetryInterval);
        });

        // Инбокс и аутбокс: статус, событие и отметка об обработке уезжают одной транзакцией.
        endpointConfigurator.UseEntityFrameworkOutbox<MeteoExportDbContext>(registrationContext);

        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbit)
        {
            rabbit.SetQuorumQueue();
        }
    }
}
