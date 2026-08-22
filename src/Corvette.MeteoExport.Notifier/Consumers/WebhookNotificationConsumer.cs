using Corvette.MeteoExport.Contracts;
using Corvette.MeteoExport.Notifier.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Notifier.Consumers;

/// <summary>
/// Шлёт вебхук по событию о завершении задания.
/// </summary>
public class WebhookNotificationConsumer : IConsumer<ExportFinishedMessage>
{
    private readonly WebhookSender _sender;
    private readonly ILogger<WebhookNotificationConsumer> _logger;

    public WebhookNotificationConsumer(WebhookSender sender, ILogger<WebhookNotificationConsumer> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<ExportFinishedMessage> consumeContext)
    {
        ArgumentNullException.ThrowIfNull(consumeContext);

        var message = consumeContext.Message;
        if (message.JobId == Guid.Empty) throw new ArgumentException("В событии нет идентификатора задания.", nameof(consumeContext));

        if (string.IsNullOrWhiteSpace(message.WebhookUrl))
        {
            _logger.LogDebug($"Вебхук не заказан, отправлять некуда (JobId=\"{message.JobId}\")");
            return;
        }

        // Идентификатор доставки должен пережить повтор, поэтому берём его из сообщения.
        var deliveryId = consumeContext.MessageId ?? throw new ArgumentException("В событии нет идентификатора сообщения.", nameof(consumeContext));

        await _sender.SendAsync(message, deliveryId, consumeContext.CancellationToken);
    }
}
