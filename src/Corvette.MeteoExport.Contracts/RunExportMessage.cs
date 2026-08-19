namespace Corvette.MeteoExport.Contracts;

/// <summary>
/// Команда «выполнить выгрузку».
/// </summary>
public class RunExportMessage
{
    public required Guid JobId { get; init; }
}
