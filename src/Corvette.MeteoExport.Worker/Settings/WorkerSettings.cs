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

    /// <summary>
    /// Параметры хранилища файлов.
    /// </summary>
    public required StorageSettings Storage { get; init; }

    /// <summary>
    /// Параметры обращения к поставщику погоды.
    /// </summary>
    public required OpenMeteoSettings OpenMeteo { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) throw new InvalidOperationException("Не задана строка подключения к базе (ConnectionString).");
        if (Rabbit == null) throw new InvalidOperationException("В конфигурации нет секции 'Rabbit'.");
        if (Storage == null) throw new InvalidOperationException("В конфигурации нет секции 'Storage'.");
        if (OpenMeteo == null) throw new InvalidOperationException("В конфигурации нет секции 'OpenMeteo'.");

        Rabbit.Validate();
        Storage.Validate();
        OpenMeteo.Validate();
    }
}
