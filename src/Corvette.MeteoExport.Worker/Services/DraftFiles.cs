using Corvette.MeteoExport.Worker.Settings;
using Microsoft.Extensions.Logging;

namespace Corvette.MeteoExport.Worker.Services;

/// <summary>
/// Владеет каталогом черновиков: соглашение об именах файлов и уборка живут только здесь.
/// </summary>
public class DraftFiles
{
    private const string DraftExtension = ".csv";

    private readonly StorageSettings _settings;
    private readonly ILogger<DraftFiles> _logger;

    public DraftFiles(StorageSettings settings, ILogger<DraftFiles> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Готовит каталог к работе, очищая его от всего, что осталось с прошлого запуска.
    /// </summary>
    public void Prepare()
    {
        // В каталоге лежат обрывки заданий, прерванных прошлым запуском; все они будут выполнены заново.
        if (Directory.Exists(_settings.DraftDirectory))
        {
            Directory.Delete(_settings.DraftDirectory, recursive: true);
        }

        Directory.CreateDirectory(_settings.DraftDirectory);

        _logger.LogInformation($"Каталог черновиков очищен (DraftDirectory=\"{_settings.DraftDirectory}\")");
    }

    /// <summary>
    /// Путь к черновику задания.
    /// </summary>
    public string GetPath(Guid jobId) => Path.Combine(_settings.DraftDirectory, $"{jobId}{DraftExtension}");

    /// <summary>
    /// Удаляет черновик задания.
    /// </summary>
    public void Delete(Guid jobId)
    {
        File.Delete(GetPath(jobId));
    }
}
