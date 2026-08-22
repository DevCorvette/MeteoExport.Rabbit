namespace Corvette.MeteoExport.Notifier.Settings;

/// <summary>
/// Настройки сервиса уведомлений.
/// </summary>
public class NotifierSettings
{
    /// <summary>
    /// Параметры подключения к брокеру.
    /// </summary>
    public required RabbitSettings Rabbit { get; init; }

    /// <summary>
    /// Параметры отправки почты.
    /// </summary>
    public required EmailSettings Email { get; init; }

    public void Validate()
    {
        if (Rabbit == null) throw new InvalidOperationException("В конфигурации нет секции 'Rabbit'.");
        if (Email == null) throw new InvalidOperationException("В конфигурации нет секции 'Email'.");

        Rabbit.Validate();
        Email.Validate();
    }
}
