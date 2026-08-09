namespace Corvette.MeteoExport.Api.Settings;

/// <summary>
/// Параметры подключения к брокеру.
/// </summary>
public class RabbitSettings
{
    /// <summary>
    /// Адрес брокера.
    /// </summary>
    public required string HostName { get; init; }

    /// <summary>
    /// Порт AMQP.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Пользователь брокера.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Пароль пользователя.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Виртуальный хост брокера.
    /// </summary>
    public string VirtualHost { get; init; } = "/";

    /// <summary>
    /// Сколько ждать подтверждения публикации.
    /// </summary>
    public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName)) throw new InvalidOperationException("Не задан адрес брокера (HostName).");
        if (Port <= 0) throw new InvalidOperationException($"Порт брокера должен быть положительным (Port={Port}).");
        if (string.IsNullOrWhiteSpace(UserName)) throw new InvalidOperationException("Не задан пользователь брокера (UserName).");
        if (string.IsNullOrWhiteSpace(Password)) throw new InvalidOperationException("Не задан пароль пользователя брокера (Password).");
        if (string.IsNullOrWhiteSpace(VirtualHost)) throw new InvalidOperationException("Не задан виртуальный хост брокера (VirtualHost).");
        if (PublishTimeout <= TimeSpan.Zero) throw new InvalidOperationException($"Таймаут публикации должен быть положительным (PublishTimeout=\"{PublishTimeout}\").");
    }
}
