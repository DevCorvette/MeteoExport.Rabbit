namespace Corvette.MeteoExport.Api.Settings;

/// <summary>
/// Настройки сервиса приёма запросов.
/// </summary>
public class ApiSettings
{
    /// <summary>
    /// Строка подключения к базе.
    /// </summary>
    public required string ConnectionString { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) throw new InvalidOperationException("Не задана строка подключения к базе (ConnectionString).");
    }
}
