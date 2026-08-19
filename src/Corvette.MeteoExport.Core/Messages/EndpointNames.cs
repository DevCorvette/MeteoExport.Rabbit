namespace Corvette.MeteoExport.Core.Messages;

/// <summary>
/// Имена точек приёма
/// </summary>
public static class EndpointNames
{
    /// <summary>
    /// Команды на выполнение выгрузки, <see cref="RunExportMessage"/>.
    /// </summary>
    public const string Exports = "exports";

    /// <summary>
    /// Команды на подведение итога, <see cref="FinishExportMessage"/>.
    /// </summary>
    public const string ExportsFinish = "exports.finish";

    /// <summary>
    /// Сообщения о сбое обработки выгрузки.
    /// </summary>
    public const string ExportsFault = "exports.fault";
}
