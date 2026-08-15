using System.Text.Json;
using System.Text.Json.Serialization;

namespace Corvette.MeteoExport.Worker.OpenMeteo;

/// <summary>
/// Блок hourly из ответа Open-Meteo — почасовые ряды в одной точке.
/// </summary>
/// <remarks>
/// Приходит колонками: каждая величина — свой массив, а час — общий для всех индекс.
/// </remarks>
public class HourlyWeather
{
    /// <summary>
    /// Отметки времени ряда, UTC, вида 1990-01-01T00:00.
    /// </summary>
    [JsonPropertyName("time")]
    public List<string> Time { get; init; } = [];

    /// <summary>
    /// Ряды значений по именам величин.
    /// </summary>
    /// <remarks>
    /// Имена величин приносит запрос, поэтому свойствами их не описать — забираем всё, что осталось
    /// неразобранным. Значения не приводим к double: сырой текст числа переносится в CSV без потерь.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Values { get; init; } = [];
}
