using System.Text.Json.Serialization;

namespace Corvette.MeteoExport.Worker.OpenMeteo;

/// <summary>
/// Ответ Open-Meteo по одной точке.
/// </summary>
public class LocationResponse
{
    /// <summary>
    /// Почасовые ряды.
    /// </summary>
    [JsonPropertyName("hourly")]
    public HourlyWeather? Hourly { get; init; }
}
