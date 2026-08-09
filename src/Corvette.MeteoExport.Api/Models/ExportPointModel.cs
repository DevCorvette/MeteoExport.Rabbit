using Corvette.MeteoExport.Core.Models;

namespace Corvette.MeteoExport.Api.Models;

/// <summary>
/// Точка на карте в запросе на выгрузку.
/// </summary>
public class ExportPointModel
{
    /// <summary>
    /// Широта в градусах.
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Долгота в градусах.
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Подпись точки — попадает в колонку итогового CSV.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Переносит точку в вид, в котором она ложится в базу.
    /// </summary>
    public ExportPoint ToPoint() =>
        new()
        {
            Latitude = Latitude,
            Longitude = Longitude,
            Name = Name,
        };
}
