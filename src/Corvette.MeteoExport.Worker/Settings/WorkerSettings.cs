namespace Corvette.MeteoExport.Worker.Settings;

/// <summary>
/// Настройки сервиса выгрузки.
/// </summary>
public class WorkerSettings
{
    /// <summary>
    /// Строка подключения к базе.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Параметры подключения к брокеру.
    /// </summary>
    public required RabbitSettings Rabbit { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) throw new InvalidOperationException("Не задана строка подключения к базе (ConnectionString).");
        if (Rabbit == null) throw new InvalidOperationException("В конфигурации нет секции 'Rabbit'.");

        Rabbit.Validate();
    }
}
