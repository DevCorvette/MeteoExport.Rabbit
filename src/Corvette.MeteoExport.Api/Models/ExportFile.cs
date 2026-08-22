using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Corvette.MeteoExport.Api.Settings;
using Corvette.MeteoExport.Contracts;
using Corvette.MeteoExport.Core;
using Microsoft.EntityFrameworkCore;

namespace Corvette.MeteoExport.Api.Models;

/// <summary>
/// Готовый файл выгрузки: откуда его читать и почему его может не быть.
/// </summary>
public class ExportFile
{
    private const string FileExtension = ".csv";

    /// <summary>
    /// Задание, файл которого запрошен.
    /// </summary>
    public required Guid JobId { get; init; }

    /// <summary>
    /// Стадия задания — ею объясняем клиенту, почему файла ещё нет.
    /// </summary>
    public ExportStatus JobStatus { get; private set; }

    /// <summary>
    /// Содержимое файла; заполнено только у <see cref="ExportFileState.Ready"/>.
    /// </summary>
    public Stream? Content { get; private set; }

    /// <summary>
    /// Имя, под которым файл уедет клиенту.
    /// </summary>
    public string FileName => $"{JobId}{FileExtension}";

    /// <summary>
    /// Загружает всё, что нужно для ответа: состояние задания и сам объект из хранилища.
    /// </summary>
    public async Task<ExportFileState> InitAsync(
        MeteoExportDbContext context,
        AmazonS3Client storage,
        StorageSettings storageSettings,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(storageSettings);
        ArgumentNullException.ThrowIfNull(logger);

        // Точки и величины задания тут не нужны, а лежат они в jsonb — читаем только нужные колонки.
        var job = await context.ExportJobs
            .AsNoTracking()
            .Where(x => x.Id == JobId)
            .Select(x => new { x.Status, x.ResultFilePath })
            .SingleOrDefaultAsync(cancellationToken);

        if (job == null)
            return ExportFileState.NotFound;

        JobStatus = job.Status;
        if (job.Status != ExportStatus.Completed)
            return ExportFileState.NotReady;

        var key = job.ResultFilePath ?? throw new InvalidOperationException($"Задание завершено, но ключа файла у него нет (JobId=\"{JobId}\").");

        var request = new GetObjectRequest
        {
            BucketName = storageSettings.Bucket,
            Key = key,
        };

        try
        {
            var response = await storage.GetObjectAsync(request, cancellationToken);
            Content = response.ResponseStream;

            return ExportFileState.Ready;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning($"Файла завершённого задания нет в хранилище (JobId=\"{JobId}\", Key=\"{key}\")");
            return ExportFileState.NotFound;
        }
    }
}
