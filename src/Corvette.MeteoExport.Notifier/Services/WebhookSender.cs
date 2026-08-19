using Corvette.MeteoExport.Contracts;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Notifier.Services;

/// <summary>
/// Отправляет вебхук о завершении задания.
/// </summary>
public class WebhookSender
{
    private readonly ILogger<WebhookSender> _logger;

    public WebhookSender(ILogger<WebhookSender> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Отправляет вебхук по событию о завершении.
    /// </summary>
    public Task SendAsync(ExportFinishedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // заглушка
        _logger.LogInformation($"Вебхук не отправлен, работает заглушка (WebhookUrl=\"{message.WebhookUrl}\", JobId=\"{message.JobId}\", Status=\"{message.Status}\", FinishedAt=\"{message.FinishedAt:O}\")");

        return Task.CompletedTask;
    }
}
