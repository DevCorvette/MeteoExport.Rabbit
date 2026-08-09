namespace Corvette.MeteoExport.Api.Models;

/// <summary>
/// Ответ на запрос выгрузки.
/// </summary>
public class CreateExportResponse
{
    /// <summary>
    /// Идентификатор задания — по нему спрашивают состояние.
    /// </summary>
    public required Guid JobId { get; init; }
}
