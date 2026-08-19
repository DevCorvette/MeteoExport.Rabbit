namespace Corvette.MeteoExport.Contracts;

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

    /// <summary>
    /// Письма о завершении задания.
    /// </summary>
    public const string NotifyEmail = "notify.email";

    /// <summary>
    /// Вебхуки о завершении задания.
    /// </summary>
    public const string NotifyWebhook = "notify.webhook";
}
