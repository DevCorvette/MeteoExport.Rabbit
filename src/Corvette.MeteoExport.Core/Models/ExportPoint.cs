namespace Corvette.MeteoExport.Core.Models;

/// <summary>
/// Точка на карте, для которой заказана выгрузка.
/// </summary>
public class ExportPoint
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }

    /// <summary>
    /// Подпись точки — попадает в колонку итогового CSV.
    /// </summary>
    public string? Name { get; init; }
}
