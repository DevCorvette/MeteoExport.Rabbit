namespace Corvette.MeteoExport.Contracts;

/// <summary>
/// Событие «задание завершено» — повод отправить письмо и вебхук.
/// </summary>
public class ExportFinishedMessage
{
    public required Guid JobId { get; init; }

    public required ExportStatus Status { get; init; }

    public required DateTime FinishedAt { get; init; }

    /// <summary>
    /// Адрес для письма, если заказан.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Адрес вебхука, если заказан.
    /// </summary>
    public string? WebhookUrl { get; init; }

    /// <summary>
    /// Текст ошибки, с которой задание провалилось.
    /// </summary>
    public string? Error { get; init; }
}
