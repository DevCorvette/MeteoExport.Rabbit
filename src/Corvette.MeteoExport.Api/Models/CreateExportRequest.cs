using System.Globalization;
using System.Net.Mail;
using Corvette.MeteoExport.Api.Services;
using Corvette.MeteoExport.Core;
using Corvette.MeteoExport.Core.Entities;
using Corvette.MeteoExport.Core.Models;
using Corvette.MeteoExport.Messaging.Messages;
using Corvette.MeteoExport.Messaging.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Corvette.MeteoExport.Api.Models;

/// <summary>
/// Запрос на выгрузку исторической погоды.
/// </summary>
public class CreateExportRequest
{
    private const int MaxPoints = 100;

    /// <summary>
    /// На сколько суток архив отстаёт от сегодняшнего дня.
    /// </summary>
    private const int ArchiveLagDays = 5;

    /// <summary>
    /// Потолок объёма запроса: точки × сутки × величины.
    /// </summary>
    private const long MaxCells = 1_000_000;

    private static readonly DateOnly ArchiveStart = new(1940, 1, 1);

    /// <summary>
    /// Что разрешено заказывать — имена почасовых параметров Open-Meteo.
    /// </summary>
    internal static readonly string[] AllowedVariables =
    [
        "temperature_2m",
        "relative_humidity_2m",
        "dew_point_2m",
        "apparent_temperature",
        "precipitation",
        "rain",
        "snowfall",
        "pressure_msl",
        "cloud_cover",
        "wind_speed_10m",
        "wind_direction_10m",
    ];

    private static readonly string[] DefaultVariables = ["temperature_2m", "precipitation"];

    /// <summary>
    /// Точки, для которых нужна выгрузка.
    /// </summary>
    public ExportPointModel[] Points { get; init; } = [];

    /// <summary>
    /// Первые сутки диапазона включительно.
    /// </summary>
    public DateOnly From { get; init; }

    /// <summary>
    /// Последние сутки диапазона включительно.
    /// </summary>
    public DateOnly To { get; init; }

    /// <summary>
    /// Заказанные величины; не заданы — берём температуру и осадки.
    /// </summary>
    public string[]? Variables { get; init; }

    /// <summary>
    /// Адрес для письма о завершении, необязателен.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Адрес для вебхука о завершении, необязателен.
    /// </summary>
    public string? WebhookUrl { get; init; }

    /// <summary>
    /// Проверяет запрос; пустой список означает, что запрос годится.
    /// </summary>
    public IReadOnlyList<string> Validate(ILogger<CreateExportRequest> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var errors = new List<string>();

        ValidatePoints(errors);
        ValidateDates(errors);
        ValidateVariables(errors);
        ValidateNotifications(errors);
        ValidateVolume(errors);

        if (errors.Count > 0)
        {
            logger.LogWarning($"Запрос отклонён валидацией (Errors={errors.Count}, Details=\"{string.Join("; ", errors)}\")");
        }

        return errors;
    }

    private void ValidatePoints(List<string> errors)
    {
        if (Points == null || Points.Length == 0)
        {
            errors.Add("Не задана ни одна точка.");
            return;
        }

        if (Points.Length > MaxPoints)
        {
            errors.Add($"Точек больше допустимого (Points={Points.Length}, MaxPoints={MaxPoints}).");
        }

        foreach (var point in Points)
        {
            if (point.Latitude < -90 || point.Latitude > 90)
            {
                errors.Add($"Широта вне допустимых значений (Latitude={point.Latitude.ToString(CultureInfo.InvariantCulture)}).");
            }

            if (point.Longitude < -180 || point.Longitude > 180)
            {
                errors.Add($"Долгота вне допустимых значений (Longitude={point.Longitude.ToString(CultureInfo.InvariantCulture)}).");
            }
        }
    }

    private void ValidateDates(List<string> errors)
    {
        if (From > To)
        {
            errors.Add($"Начало диапазона позже конца (From=\"{From:O}\", To=\"{To:O}\").");
        }

        if (From < ArchiveStart)
        {
            errors.Add($"Архив начинается позже (From=\"{From:O}\", ArchiveStart=\"{ArchiveStart:O}\").");
        }

        // Архив досчитывается с задержкой, поэтому последние несколько суток заказать нельзя.
        var lastAvailable = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-ArchiveLagDays);
        if (To > lastAvailable)
        {
            errors.Add($"Данных за этот срок в архиве ещё нет (To=\"{To:O}\", LastAvailable=\"{lastAvailable:O}\").");
        }
    }

    private void ValidateVariables(List<string> errors)
    {
        if (Variables == null)
            return;

        // По различным: повторённая опечатка — это одна ошибка, а не две.
        var unknown = Variables
            .Distinct(StringComparer.Ordinal)
            .Where(x => !AllowedVariables.Contains(x))
            .ToList();

        if (unknown.Count == 0)
            return;

        foreach (var variable in unknown)
        {
            errors.Add($"Неизвестная величина (Variable=\"{variable}\").");
        }

        // Список отдельной строкой и один раз: повторять его у каждой опечатки — шум.
        errors.Add($"Допустимые величины: {string.Join(", ", AllowedVariables)}.");
    }

    private void ValidateNotifications(List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(Email) && !MailAddress.TryCreate(Email, out _))
        {
            errors.Add($"Адрес почты не разобран (Email=\"{Email}\").");
        }

        if (string.IsNullOrWhiteSpace(WebhookUrl))
            return;

        if (!Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var webhook)
            || (webhook.Scheme != Uri.UriSchemeHttp && webhook.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add($"Адрес вебхука должен быть абсолютной http- или https-ссылкой (WebhookUrl=\"{WebhookUrl}\").");
        }
    }

    /// <summary>
    /// Проверяет, что запрос вообще выполним за разумное время.
    /// </summary>
    private void ValidateVolume(List<string> errors)
    {
        // Считать объём по заведомо битому запросу смысла нет — про это уже сказано выше.
        if (Points == null || Points.Length == 0 || From > To)
            return;

        var days = To.DayNumber - From.DayNumber + 1;
        var cells = (long)ResolvePoints().Count * days * ResolveVariables().Length;

        if (cells > MaxCells)
        {
            errors.Add($"Запрос слишком объёмный (Cells={cells}, MaxCells={MaxCells}).");
        }
    }

    /// <summary>
    /// Заводит задание и отдаёт его брокеру, а если такое же уже выполняется — возвращает его.
    /// </summary>
    public async Task<Guid> SaveAsync(
        IDbContextFactory<MeteoExportDbContext> contextFactory,
        ExportPublisher publisher,
        ILogger<CreateExportRequest> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(logger);

        var requestHash = ComputeHash();

        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var existingId = await FindActiveAsync(context, requestHash, cancellationToken);
            if (existingId != null)
            {
                logger.LogInformation($"Задание с таким отпечатком уже выполняется, новое не заводим (JobId=\"{existingId}\", RequestHash=\"{requestHash}\")");
                return existingId.Value;
            }

            // сохраняем
            var job = new ExportJobEntity
            {
                Id = Guid.CreateVersion7(),
                Status = ExportStatus.Queued,
                RequestHash = requestHash,
                Points = ResolvePoints().Select(x => x.ToPoint()).ToList(),
                FromDate = From,
                ToDate = To,
                Variables = ResolveVariables(),
                Email = Email?.Trim(),
                WebhookUrl = WebhookUrl?.Trim(),
            };

            context.ExportJobs.Add(job);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // два одинаковых запроса
                // текущий получается лишним
                context.Entry(job).State = EntityState.Detached;

                var rivalId = await FindActiveAsync(context, requestHash, cancellationToken)
                    ?? throw new InvalidOperationException($"Индекс отверг задание как дубль, но самого задания нет (RequestHash=\"{requestHash}\").");

                logger.LogInformation($"Задание с таким отпечатком завели параллельно, возвращаем его (JobId=\"{rivalId}\", RequestHash=\"{requestHash}\")");
                
                // дальше делать нечего - выходим
                return rivalId;
            }

            logger.LogInformation($"Задание принято (JobId=\"{job.Id}\", Points={job.Points.Count}, Variables={job.Variables.Length}, From=\"{From:O}\", To=\"{To:O}\")");

            // публикуем событие
            var successPublish = await publisher.PublishAsync(new RunExportMessage { JobId = job.Id }, cancellationToken);
            if (successPublish)
            {
                job.PublishedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
            }

            return job.Id;
        }
    }

    /// <summary>
    /// Отпечаток запроса: у двух запросов, означающих одно и то же, он совпадает.
    /// </summary>
    internal string ComputeHash()
    {
        var fingerprint = new FingerprintBuilder();

        // точки
        var sortedPoints = ResolvePoints()
            .OrderBy(x => x.Latitude)
            .ThenBy(x => x.Longitude)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        fingerprint.Append(sortedPoints.Count);
        foreach (var point in sortedPoints)
        {
            fingerprint.Append(point.Latitude);
            fingerprint.Append(point.Longitude);
            fingerprint.Append(point.Name);
        }

        // диапазон
        fingerprint.Append(From);
        fingerprint.Append(To);

        // величины: умолчания подставляем до отпечатка — запрос без них и запрос с теми же значениями
        // явно означают одно и то же
        var sortedVariables = ResolveVariables()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        fingerprint.Append(sortedVariables.Count);
        foreach (var variable in sortedVariables)
        {
            fingerprint.Append(variable);
        }

        // адреса уведомлений: на сам файл они не влияют, а на то, кому он достанется, — влияют
        fingerprint.Append(Email?.Trim().ToLowerInvariant());
        fingerprint.Append(WebhookUrl?.Trim());

        return fingerprint.ToHash();
    }

    /// <summary>
    /// Ищет незавершённое задание с таким же отпечатком.
    /// </summary>
    private static async Task<Guid?> FindActiveAsync(MeteoExportDbContext context, string requestHash, CancellationToken cancellationToken) =>
        await context.ExportJobs
            .AsNoTracking()
            .Where(x => x.RequestHash == requestHash && (x.Status == ExportStatus.Queued || x.Status == ExportStatus.Running))
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Точки запроса без повторов по координатам.
    /// </summary>
    private IReadOnlyList<ExportPointModel> ResolvePoints() =>
        [.. Points.DistinctBy(x => (x.Latitude, x.Longitude))];

    /// <summary>
    /// Заказанные величины без повторов, а если клиент их не выбрал — набор по умолчанию.
    /// </summary>
    private string[] ResolveVariables()
    {
        if (Variables == null || Variables.Length == 0)
            return [.. DefaultVariables];

        return [.. Variables.Distinct(StringComparer.Ordinal)];
    }
}
