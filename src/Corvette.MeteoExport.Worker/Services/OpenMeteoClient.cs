using System.Globalization;
using System.Net;
using System.Text.Json;
using Corvette.MeteoExport.Core.Models;
using Corvette.MeteoExport.Worker.OpenMeteo;
using Corvette.MeteoExport.Worker.Settings;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace Corvette.MeteoExport.Worker.Services;

/// <summary>
/// Клиент Open-Meteo: запрашивает почасовой архив сразу по пачке точек.
/// </summary>
public class OpenMeteoClient : IDisposable
{
    private const string ArchiveResource = "/v1/archive";

    /// <summary>
    /// Формат даты в параметрах start_date и end_date.
    /// </summary>
    private const string DateFormat = "yyyy-MM-dd";

    private const int MaxAttempts = 3;
    private const int MaxErrorContentLength = 300;

    private static readonly TimeSpan RetryDelayStep = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Архив за год почасово поставщик собирает дольше прогноза, отсюда и запас.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(120);

    private readonly RestClient _client;
    private readonly ILogger<OpenMeteoClient> _logger;

    public OpenMeteoClient(OpenMeteoSettings settings, ILogger<OpenMeteoClient> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _client = new RestClient(new RestClientOptions(settings.BaseUrl)
        {
            Timeout = RequestTimeout,
        });
    }

    /// <summary>
    /// Возвращает почасовые ряды за отрезок дат по каждой точке в том же порядке, в каком точки переданы.
    /// </summary>
    public async Task<IReadOnlyList<LocationResponse>> LoadAsync(
        IReadOnlyList<ExportPoint> points,
        DateOnly from,
        DateOnly to,
        string[] variables,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(variables);
        if (points.Count == 0) throw new ArgumentException("Пачка точек пуста.", nameof(points));
        if (variables.Length == 0) throw new ArgumentException("Не заданы величины.", nameof(variables));

        var request = new RestRequest(ArchiveResource)
            .AddQueryParameter("latitude", JoinCoordinates(points.Select(x => x.Latitude)), encode: false)
            .AddQueryParameter("longitude", JoinCoordinates(points.Select(x => x.Longitude)), encode: false)
            .AddQueryParameter("start_date", from.ToString(DateFormat, CultureInfo.InvariantCulture))
            .AddQueryParameter("end_date", to.ToString(DateFormat, CultureInfo.InvariantCulture))
            .AddQueryParameter("hourly", string.Join(',', variables), encode: false)
            .AddQueryParameter("timezone", "UTC");

        _logger.LogDebug($"Запрос архива погоды (Points={points.Count}, From=\"{from:O}\", To=\"{to:O}\", Variables={variables.Length})");

        var content = await ExecuteWithRetryAsync(request, cancellationToken);

        return Parse(content, points.Count);
    }

    private static string JoinCoordinates(IEnumerable<double> coordinates) =>
        string.Join(',', coordinates.Select(x => x.ToString(CultureInfo.InvariantCulture)));

    private async Task<string> ExecuteWithRetryAsync(RestRequest request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await _client.ExecuteAsync(request, cancellationToken);
            if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                return response.Content;

            // Таймаут и обрыв связи HTTP-статуса не имеют вовсе. 429 — превышен лимит, 5xx — временные
            // проблемы сервиса. На 400 повторять бессмысленно: параметры запроса от повтора не изменятся.
            var isRetryable = response.ResponseStatus == ResponseStatus.TimedOut
                              || response.ResponseStatus == ResponseStatus.Error
                              || response.StatusCode == HttpStatusCode.TooManyRequests
                              || (int)response.StatusCode >= 500;

            var reason = DescribeFailure(response);
            if (attempt >= MaxAttempts || !isRetryable)
                throw new InvalidOperationException($"Запрос архива погоды не выполнен: {reason}");

            // Пауза растёт линейно с номером попытки
            var delay = RetryDelayStep * attempt;
            _logger.LogWarning($"Запрос архива погоды не удался, повторяем (Attempt={attempt}, DelayMs={delay.TotalMilliseconds:F0}, Reason=\"{reason}\")");
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static string DescribeFailure(RestResponse response)
    {
        if (response.ErrorException != null)
            return $"{response.ResponseStatus}, {response.ErrorException.Message}";

        var content = response.Content ?? string.Empty;
        if (content.Length > MaxErrorContentLength)
            content = content[..MaxErrorContentLength];

        return $"HTTP {(int)response.StatusCode}, {content}";
    }

    /// <summary>
    /// Разбирает ответ, который для одной точки приходит объектом, а для нескольких — массивом,
    /// и возвращает точки в том же порядке, в каком они были запрошены.
    /// </summary>
    private static LocationResponse[] Parse(string content, int expectedCount)
    {
        LocationResponse[] responses;
        if (content.TrimStart().StartsWith('['))
        {
            responses = JsonSerializer.Deserialize<LocationResponse[]>(content) ?? [];
        }
        else
        {
            var single = JsonSerializer.Deserialize<LocationResponse>(content);
            responses = single == null ? [] : [single];
        }

        // проверим что отдали ровно столько же как и запросили
        if (responses.Length != expectedCount)
            throw new InvalidOperationException($"Open-Meteo вернул другое количество точек (Requested={expectedCount}, Received={responses.Length}).");

        return responses;
    }

    public void Dispose() => _client.Dispose();
}
