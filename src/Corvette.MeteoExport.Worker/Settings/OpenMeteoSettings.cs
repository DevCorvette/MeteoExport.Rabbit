namespace Corvette.MeteoExport.Worker.Settings;

/// <summary>
/// Параметры доступа к сервису погоды Open-Meteo.
/// </summary>
public class OpenMeteoSettings
{
    /// <summary>
    /// Базовый адрес сервиса.
    /// </summary>
    public required string BaseUrl { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) throw new InvalidOperationException("Не задан базовый адрес Open-Meteo (BaseUrl).");
    }
}
