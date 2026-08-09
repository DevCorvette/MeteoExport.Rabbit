using System.Net.Mime;
using System.Text.Json;
using Corvette.MeteoExport.Api.Settings;
using Corvette.MeteoExport.Core.Messages;
using RabbitMQ.Client;

namespace Corvette.MeteoExport.Api.Services;

/// <summary>
/// Объявляет топологию брокера и публикует в неё команды на выполнение выгрузки.
/// </summary>
public class ExportPublisher : IAsyncDisposable
{
    /// <summary>
    /// Сколько раз брокер отдаст сообщение потребителю, прежде чем признать его отравленным.
    /// </summary>
    private const int DeliveryLimit = 5;

    private const string ClientName = "meteoexport-api";

    private readonly RabbitSettings _settings;
    private readonly ILogger<ExportPublisher> _logger;

    /// <summary>
    /// Пускает к открытию соединения по одному
    /// </summary>
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public ExportPublisher(RabbitSettings settings, ILogger<ExportPublisher> logger)
    {
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
                    ContentType = MediaTypeNames.Application.Json,
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

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _connectionLock.Dispose();
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        var current = _channel;
        if (current != null && current.IsOpen)
            return current;

        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            // Пока ждали очереди, канал мог открыть сосед.
            if (_channel != null && _channel.IsOpen)
                return _channel;

            await CloseAsync();

            // соединение
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                // Видно в списке соединений брокера.
                ClientProvidedName = ClientName,
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            // канал
            // Подтверждения включаются при создании канала и после уже не меняются.
            var options = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
            var channel = await _connection.CreateChannelAsync(options, cancellationToken);

            await DeclareAsync(channel, cancellationToken);

            // В поле — после объявления: быстрый путь выше отдаёт канал без блокировки.
            _channel = channel;

            _logger.LogInformation($"Топология объявлена (Host=\"{_settings.HostName}\", Port={_settings.Port}, VirtualHost=\"{_settings.VirtualHost}\", Queue=\"{ExportTopology.ExportsQueue}\")");

            return channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            ExportTopology.ExportsExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            // Quorum: очередь заданий реплицируется и переживает потерю узла.
            ["x-queue-type"] = "quorum",
            // Считать доставки — работа брокера.
            ["x-delivery-limit"] = DeliveryLimit,
            ["x-dead-letter-exchange"] = ExportTopology.RetryExchange,
        };

        await channel.QueueDeclareAsync(
            ExportTopology.ExportsQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            ExportTopology.ExportsQueue,
            ExportTopology.ExportsExchange,
            ExportTopology.ExportsRoutingKey,
            cancellationToken: cancellationToken);
    }

    private async Task CloseAsync()
    {
        if (_channel != null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
