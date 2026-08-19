using Corvette.MeteoExport.Contracts;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Notifier.Services;

/// <summary>
/// Отправляет письмо о завершении задания.
/// </summary>
public class EmailSender
{
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Отправляет письмо по событию о завершении.
    /// </summary>
    public Task SendAsync(ExportFinishedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // заглушка 
        _logger.LogInformation($"Письмо не отправлено, работает заглушка (Email=\"{message.Email}\", JobId=\"{message.JobId}\", Status=\"{message.Status}\", Error=\"{message.Error}\")");

        return Task.CompletedTask;
    }
}
