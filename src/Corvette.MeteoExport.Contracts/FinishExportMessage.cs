namespace Corvette.MeteoExport.Contracts;

/// <summary>
/// Команда «подвести итог выгрузки».
/// </summary>
public class FinishExportMessage
{
    public required Guid JobId { get; init; }

    /// <summary>
    /// Ключ готового файла в хранилище
    /// пуст, если выгрузка не удалась.
    /// </summary>
    public string? ResultFilePath { get; init; }

    /// <summary>
    /// Текст ошибки, с которой кончилась выгрузка
    /// пуст, если всё получилось.
    /// </summary>
    public string? Error { get; init; }
}
