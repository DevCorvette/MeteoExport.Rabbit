namespace Corvette.MeteoExport.Core.Messages;

/// <summary>
/// Команда «выполнить выгрузку».
/// </summary>
public class RunExportMessage
{
    public required Guid JobId { get; init; }
}
