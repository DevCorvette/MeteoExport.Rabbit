using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Corvette.MeteoExport.Contracts;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace Corvette.MeteoExport.Notifier.Services;

/// <summary>
/// Отправляет вебхук о завершении задания.
/// </summary>
public class WebhookSender : IDisposable
{
    private const int MaxErrorContentLength = 300;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly RestClient _client;
    private readonly ILogger<WebhookSender> _logger;

    public WebhookSender(ILogger<WebhookSender> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Базового адреса у клиента нет: каждое событие приносит свой адрес вебхука.
        _client = new RestClient(new RestClientOptions
        {
            Timeout = RequestTimeout,
        });
    }

    /// <summary>
    /// Отправляет вебхук по событию о завершении.
    /// </summary>
    /// <param name="deliveryId">Идентификатор доставки, по которому получатель отбрасывает дубли.</param>
    public async Task SendAsync(ExportFinishedMessage message, Guid deliveryId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.WebhookUrl)) throw new ArgumentException("В событии нет адреса вебхука.", nameof(message));

        var payload = new WebhookPayload
        {
            JobId = message.JobId,
            Status = message.Status.ToString(),
            FinishedAt = message.FinishedAt,
            Error = message.Error,
        };

        var request = new RestRequest(message.WebhookUrl, Method.Post)
            .AddHeader("X-Delivery-Id", deliveryId.ToString())
            .AddStringBody(JsonSerializer.Serialize(payload, SerializerOptions), ContentType.Json);

        // отправим
        var response = await _client.ExecuteAsync(request, cancellationToken);
        if (!response.IsSuccessful)
        {
            var content = response.Content ?? string.Empty;
            if (content.Length > MaxErrorContentLength)
            {
                content = content[..MaxErrorContentLength];
            }

            var reason = (int)response.StatusCode == 0
                ? $"{response.ResponseStatus}, {response.ErrorException?.Message}"
                : $"HTTP {(int)response.StatusCode}, {content}";

            throw new InvalidOperationException($"Вебхук не доставлен (WebhookUrl=\"{message.WebhookUrl}\", JobId=\"{message.JobId}\", Reason=\"{reason}\").");
        }

        _logger.LogInformation($"Вебхук отправлен (WebhookUrl=\"{message.WebhookUrl}\", JobId=\"{message.JobId}\", Status=\"{message.Status}\", DeliveryId=\"{deliveryId}\")");
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>
/// Тело вебхука.
/// </summary>
internal record WebhookPayload
{
    public required Guid JobId { get; init; }

    public required string Status { get; init; }

    public required DateTime FinishedAt { get; init; }

    public string? Error { get; init; }
}
