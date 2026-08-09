namespace Corvette.MeteoExport.Core.Messages;

/// <summary>
/// Имена обменников, очередей и ключей маршрутизации.
/// </summary>
public static class ExportTopology
{
    /// <summary>
    /// Обменник команд на выполнение, тип direct.
    /// </summary>
    public const string ExportsExchange = "exports";

    /// <summary>
    /// Очередь, из которой воркеры разбирают <see cref="RunExportMessage"/>.
    /// </summary>
    public const string ExportsQueue = "exports";

    /// <summary>
    /// Ключ маршрутизации команды на выполнение.
    /// </summary>
    public const string ExportsRoutingKey = "exports";

    /// <summary>
    /// Обменник, в который брокер отправляет сообщения, отвергнутые воркером.
    /// </summary>
    public const string RetryExchange = "exports.retry";
}
