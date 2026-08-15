using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Corvette.MeteoExport.Worker.Settings;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Services;

/// <summary>
/// Кладёт готовые файлы в объектное хранилище.
/// </summary>
public class ResultStorage : IDisposable
{
    private const string CsvContentType = "text/csv";

    /// <summary>
    /// Каталог в хранилище, в котором лежат готовые файлы.
    /// </summary>
    private const string ResultKeyPrefix = "exports";

    private const string ResultExtension = ".csv";

    private readonly StorageSettings _settings;
    private readonly ILogger<ResultStorage> _logger;
    private readonly AmazonS3Client _client;

    public ResultStorage(StorageSettings settings, ILogger<ResultStorage> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var configuration = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            // Корзина в пути, а не в имени хоста: доменов вида bucket.localhost у стенда нет.
            ForcePathStyle = true,
        };

        _client = new AmazonS3Client(new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey), configuration);
    }

    /// <summary>
    /// Заводит корзину, если её ещё нет.
    /// </summary>
    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (await AmazonS3Util.DoesS3BucketExistV2Async(_client, _settings.Bucket))
            return;

        await _client.PutBucketAsync(new PutBucketRequest { BucketName = _settings.Bucket }, cancellationToken);

        _logger.LogInformation($"Корзина создана (Bucket=\"{_settings.Bucket}\", Endpoint=\"{_settings.Endpoint}\")");
    }

    /// <summary>
    /// Заливает файл задания целиком.
    /// </summary>
    /// <returns>
    /// Ключ, под которым объект лёг в хранилище.
    /// </returns>
    public async Task<string> UploadAsync(string filePath, Guid jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Не задан путь к файлу.", nameof(filePath));

        var key = $"{ResultKeyPrefix}/{jobId}{ResultExtension}";

        var request = new PutObjectRequest
        {
            BucketName = _settings.Bucket,
            Key = key,
            FilePath = filePath,
            ContentType = CsvContentType,
        };

        await _client.PutObjectAsync(request, cancellationToken);

        _logger.LogInformation($"Файл залит в хранилище (Bucket=\"{_settings.Bucket}\", Key=\"{key}\", Size={new FileInfo(filePath).Length})");

        return key;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
