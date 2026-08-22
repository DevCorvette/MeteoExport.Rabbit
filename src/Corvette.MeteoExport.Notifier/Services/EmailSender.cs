using System.Text;
using Corvette.MeteoExport.Contracts;
using Corvette.MeteoExport.Notifier.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;

namespace Corvette.MeteoExport.Notifier.Services;

/// <summary>
/// Отправляет письмо о завершении задания.
/// </summary>
public class EmailSender
{
    private const string SubjectCompleted = "Выгрузка погоды готова";
    private const string SubjectFailed = "Выгрузка погоды не удалась";

    private readonly EmailSettings _settings;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(EmailSettings settings, ILogger<EmailSender> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Отправляет письмо по событию о завершении.
    /// </summary>
    /// <param name="deliveryId">Идентификатор доставки, он же Message-Id письма.</param>
    public async Task SendAsync(ExportFinishedMessage message, Guid deliveryId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.Email)) throw new ArgumentException("В событии нет адреса получателя.", nameof(message));

        if (!_settings.Enabled)
        {
            _logger.LogInformation($"Отправка почты выключена, письма не будет (Email=\"{message.Email}\", JobId=\"{message.JobId}\", Status=\"{message.Status}\")");
            return;
        }

        // текст
        var text = new StringBuilder();
        text.AppendLine(message.Status == ExportStatus.Completed
            ? "Выгрузка исторической погоды готова."
            : "Выгрузка исторической погоды не удалась.");
        text.AppendLine();
        text.AppendLine($"Задание: {message.JobId}");
        text.AppendLine($"Завершено: {message.FinishedAt:yyyy-MM-dd HH:mm:ss} UTC");

        if (!string.IsNullOrWhiteSpace(message.Error))
        {
            text.AppendLine($"Ошибка: {message.Error}");
        }

        // письмо
        var sender = MailboxAddress.Parse(_settings.From);
        var letter = new MimeMessage
        {
            // Идентификатор доставки в Message-Id: повтор отправки получатель увидит как то же письмо.
            MessageId = $"{deliveryId:N}@{sender.Domain}",
            Subject = message.Status == ExportStatus.Completed ? SubjectCompleted : SubjectFailed,
            Body = new TextPart(TextFormat.Plain) { Text = text.ToString() },
        };

        letter.From.Add(sender);
        letter.To.Add(MailboxAddress.Parse(message.Email));

        // отправка
        using (var client = new SmtpClient())
        {
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.Auto, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_settings.UserName))
            {
                await client.AuthenticateAsync(_settings.UserName, _settings.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(letter, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }

        _logger.LogInformation($"Письмо отправлено (Email=\"{message.Email}\", JobId=\"{message.JobId}\", Status=\"{message.Status}\", DeliveryId=\"{deliveryId}\")");
    }
}
