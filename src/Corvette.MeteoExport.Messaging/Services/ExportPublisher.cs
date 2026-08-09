using System.Text.Json;
using Corvette.MeteoExport.Messaging.Messages;
using Corvette.MeteoExport.Messaging.Settings;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Corvette.MeteoExport.Messaging.Services;

/// <summary>
/// Публикует команды на выполнение выгрузки.
/// </summary>
public class ExportPublisher : IAsyncDisposable
{
    private const string JsonContentType = "application/json";

    private readonly RabbitConnection _connection;
    private readonly RabbitSettings _settings;
    private readonly ILogger<ExportPublisher> _logger;

    /// <summary>
    /// Пускает к открытию канала по одному
    /// </summary>
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    private IChannel? _channel;

    public ExportPublisher(RabbitConnection connection, RabbitSettings settings, ILogger<ExportPublisher> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Публикует команду
    /// </summary>
    /// <returns>
    /// true — брокер подтвердил приём.
    /// </returns>
    public async Task<bool> PublishAsync(RunExportMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        using (var publishCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            // Под таймаутом и подключение, а не только ожидание подтверждения.
            publishCancellation.CancelAfter(_settings.PublishTimeout);

            try
            {
                var channel = await GetChannelAsync(publishCancellation.Token);

                var properties = new BasicProperties
                {
                    // Сообщение переживает перезапуск брокера.
                    Persistent = true,
                    ContentType = JsonContentType,
                    MessageId = message.JobId.ToString(),
                };

                var body = JsonSerializer.SerializeToUtf8Bytes(message);

                // mandatory: сообщение, не попавшее ни в одну очередь, возвращается ошибкой публикации.
                await channel.BasicPublishAsync(
                    ExportTopology.ExportsExchange,
                    ExportTopology.ExportsRoutingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: publishCancellation.Token);

                _logger.LogInformation($"Команда на выполнение опубликована (JobId=\"{message.JobId}\", Queue=\"{ExportTopology.ExportsQueue}\")");

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // Отмена не от вызывающего — значит сработал наш таймаут.
                _logger.LogError($"Брокер не подтвердил приём команды (JobId=\"{message.JobId}\", PublishTimeout=\"{_settings.PublishTimeout}\")");
                return false;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Команда на выполнение не опубликована (JobId=\"{message.JobId}\")");
                return false;
            }
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        var current = _channel;
        if (current != null && current.IsOpen)
            return current;

        await _channelLock.WaitAsync(cancellationToken);

        try
        {
            // Пока ждали очереди, канал мог открыть сосед.
            if (_channel != null && _channel.IsOpen)
                return _channel;

            await CloseAsync();

            // Подтверждения включаются при создании канала и после уже не меняются.
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

            _channel = await _connection.CreateChannelAsync(options, cancellationToken);

            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _channelLock.Dispose();
    }

    private async Task CloseAsync()
    {
        if (_channel != null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }
}
