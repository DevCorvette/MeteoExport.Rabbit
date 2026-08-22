namespace Corvette.MeteoExport.Api.Models;

/// <summary>
/// Чем закончилась попытка достать файл задания.
/// </summary>
public enum ExportFileState
{
    Unknown = 0,

    /// <summary>
    /// Задания с таким идентификатором нет, либо его файла нет в хранилище.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// Задание есть, но успехом ещё не закончилось.
    /// </summary>
    NotReady = 2,

    /// <summary>
    /// Файл открыт на чтение.
    /// </summary>
    Ready = 3,
}
