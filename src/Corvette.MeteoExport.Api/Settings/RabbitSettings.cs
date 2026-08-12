namespace Corvette.MeteoExport.Api.Settings;

/// <summary>
/// Параметры подключения к брокеру.
/// </summary>
public class RabbitSettings
{
    public required string HostName { get; init; }
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
    /// Имя, под которым сервис виден в списке соединений брокера.
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// Виртуальный хост брокера.
    /// </summary>
    public string VirtualHost { get; init; } = "/";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName)) throw new InvalidOperationException("Не задан адрес брокера (HostName).");
        if (Port <= 0) throw new InvalidOperationException($"Порт брокера должен быть положительным (Port={Port}).");
        if (string.IsNullOrWhiteSpace(UserName)) throw new InvalidOperationException("Не задан пользователь брокера (UserName).");
        if (string.IsNullOrWhiteSpace(Password)) throw new InvalidOperationException("Не задан пароль пользователя брокера (Password).");
        if (string.IsNullOrWhiteSpace(ClientName)) throw new InvalidOperationException("Не задано имя клиента брокера (ClientName).");
        if (string.IsNullOrWhiteSpace(VirtualHost)) throw new InvalidOperationException("Не задан виртуальный хост брокера (VirtualHost).");
    }
}
