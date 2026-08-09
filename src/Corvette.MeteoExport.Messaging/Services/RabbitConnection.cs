using Corvette.MeteoExport.Messaging.Messages;
using Corvette.MeteoExport.Messaging.Settings;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Corvette.MeteoExport.Messaging.Services;

/// <summary>
/// Соединение с брокером, общее на процесс, и объявление топологии на нём.
/// </summary>
public class RabbitConnection : IAsyncDisposable
{
    /// <summary>
    /// Сколько раз брокер отдаст сообщение потребителю, прежде чем признать его отравленным.
    /// </summary>
    private const int DeliveryLimit = 5;

    private readonly RabbitSettings _settings;
    private readonly ILogger<RabbitConnection> _logger;

    /// <summary>
    /// Пускает к открытию соединения по одному
    /// </summary>
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;

    public RabbitConnection(RabbitSettings settings, ILogger<RabbitConnection> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Открывает канал, при необходимости подняв соединение и объявив топологию.
    /// </summary>
    public async Task<IChannel> CreateChannelAsync(CreateChannelOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var connection = await GetConnectionAsync(cancellationToken);
        return await connection.CreateChannelAsync(options, cancellationToken);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var current = _connection;
        if (current != null && current.IsOpen)
            return current;

        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            // Пока ждали очереди, соединение мог поднять сосед.
            if (_connection != null && _connection.IsOpen)
                return _connection;

            await CloseAsync();

            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                ClientProvidedName = _settings.ClientName, // Видно в списке соединений брокера.
            };

            var connection = await factory.CreateConnectionAsync(cancellationToken);

            await DeclareTopologyAsync(connection, cancellationToken);

            _connection = connection;

            _logger.LogInformation($"Соединение с брокером открыто, топология объявлена (Host=\"{_settings.HostName}\", Port={_settings.Port}, VirtualHost=\"{_settings.VirtualHost}\", ClientName=\"{_settings.ClientName}\")");

            return connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Объявляет обменники, очереди и связи между ними; повторный вызов ничего не меняет.
    /// </summary>
    private static async Task DeclareTopologyAsync(IConnection connection, CancellationToken cancellationToken)
    {
        // Канал под объявление разовый: он не переживает даже этот метод.
        var options = new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false);

        await using (var channel = await connection.CreateChannelAsync(options, cancellationToken))
        {
            await channel.ExchangeDeclareAsync(
                ExportTopology.ExportsExchange,
                ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            var arguments = new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum", // Quorum: очередь заданий реплицируется и переживает потерю узла.
                ["x-delivery-limit"] = DeliveryLimit, // Считать доставки — работа брокера.
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
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _connectionLock.Dispose();
    }

    private async Task CloseAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
