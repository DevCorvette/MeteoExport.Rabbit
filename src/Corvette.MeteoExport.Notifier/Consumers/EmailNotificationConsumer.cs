using Corvette.MeteoExport.Contracts;
using Corvette.MeteoExport.Notifier.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Notifier.Consumers;

/// <summary>
/// Шлёт письмо по событию о завершении задания.
/// </summary>
public class EmailNotificationConsumer : IConsumer<ExportFinishedMessage>
{
    private readonly EmailSender _sender;
    private readonly ILogger<EmailNotificationConsumer> _logger;

    public EmailNotificationConsumer(EmailSender sender, ILogger<EmailNotificationConsumer> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<ExportFinishedMessage> consumeContext)
    {
        ArgumentNullException.ThrowIfNull(consumeContext);

        var message = consumeContext.Message;
        if (message.JobId == Guid.Empty) throw new ArgumentException("В событии нет идентификатора задания.", nameof(consumeContext));

        if (string.IsNullOrWhiteSpace(message.Email))
        {
            _logger.LogDebug($"Почта не заказана, письма не будет (JobId=\"{message.JobId}\")");
            return;
        }

        // Идентификатор доставки должен пережить повтор, поэтому берём его из сообщения.
        var deliveryId = consumeContext.MessageId ?? throw new ArgumentException("В событии нет идентификатора сообщения.", nameof(consumeContext));

        await _sender.SendAsync(message, deliveryId, consumeContext.CancellationToken);
    }
}
