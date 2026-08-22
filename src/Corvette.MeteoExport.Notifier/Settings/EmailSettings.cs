using MimeKit;

namespace Corvette.MeteoExport.Notifier.Settings;

/// <summary>
/// Параметры отправки почты.
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// Отправлять письма или только писать их в лог.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Адрес smtp-сервера.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Порт smtp-сервера.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Пользователь smtp — пустой, если сервер не требует входа.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Пароль пользователя smtp.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Отправитель писем в виде «Имя &lt;адрес&gt;».
    /// </summary>
    public required string From { get; init; }

    public void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(Host)) throw new InvalidOperationException("Не задан адрес smtp-сервера (Host).");
        if (Port <= 0) throw new InvalidOperationException($"Порт smtp-сервера должен быть положительным (Port={Port}).");
        if (!MailboxAddress.TryParse(From, out _)) throw new InvalidOperationException($"Отправитель писем не разбирается как почтовый адрес (From=\"{From}\").");
    }
}
