namespace Corvette.MeteoExport.Contracts;

/// <summary>
/// Стадия жизненного цикла задания на выгрузку.
/// </summary>
public enum ExportStatus
{
    Unknown   = 0,
    Queued    = 1,
    Running   = 2,
    Completed = 3,
    Failed    = 4,
}
