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

    public void Validate()
    {
        if (Rabbit == null) throw new InvalidOperationException("В конфигурации нет секции 'Rabbit'.");

        Rabbit.Validate();
    }
}
