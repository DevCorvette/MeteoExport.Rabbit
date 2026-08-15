namespace Corvette.MeteoExport.Worker.Settings;

/// <summary>
/// Параметры объектного хранилища и каталога черновиков.
/// </summary>
public class StorageSettings
{
    /// <summary>
    /// Адрес S3-совместимого хранилища.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Ключ доступа.
    /// </summary>
    public required string AccessKey { get; init; }

    /// <summary>
    /// Секрет к ключу доступа.
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// Корзина, в которой лежат готовые файлы.
    /// </summary>
    public required string Bucket { get; init; }

    /// <summary>
    /// Каталог, в котором собираются черновики перед заливкой.
    /// </summary>
    /// <remarks>
    /// Абсолютный путь берём как есть, относительный считаем от каталога приложения.
    /// </remarks>
    public required string DraftDirectory
    {
        // Пустое значение отдаём как есть, о нём скажет Validate.
        get => string.IsNullOrWhiteSpace(field) ? field : Path.GetFullPath(field, AppContext.BaseDirectory);
        init;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint)) throw new InvalidOperationException("Не задан адрес хранилища (Endpoint).");
        if (string.IsNullOrWhiteSpace(AccessKey)) throw new InvalidOperationException("Не задан ключ доступа к хранилищу (AccessKey).");
        if (string.IsNullOrWhiteSpace(SecretKey)) throw new InvalidOperationException("Не задан секрет к ключу доступа (SecretKey).");
        if (string.IsNullOrWhiteSpace(Bucket)) throw new InvalidOperationException("Не задана корзина хранилища (Bucket).");
        if (string.IsNullOrWhiteSpace(DraftDirectory)) throw new InvalidOperationException("Не задан каталог черновиков (DraftDirectory).");
    }
}
