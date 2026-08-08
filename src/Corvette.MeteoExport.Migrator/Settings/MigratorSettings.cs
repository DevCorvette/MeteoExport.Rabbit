using Microsoft.Extensions.Configuration;

namespace Corvette.MeteoExport.Migrator.Settings;

/// <summary>
/// Параметры запуска мигратора.
/// </summary>
internal sealed class MigratorSettings
{
    private const string ConnectionStringKey = "ConnectionString";

    /// <summary>
    /// Строка подключения к базе.
    /// </summary>
    public required string ConnectionString { get; init; }

    public static MigratorSettings Load()
    {
        var configuration = Build();

        var connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Не задан параметр '{ConnectionStringKey}'.");

        return new MigratorSettings
        {
            ConnectionString = connectionString,
        };
    }

    private static IConfiguration Build() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            // optional: в publish-результат appsettings.json не попадает, на проде файл кладут при деплое.
            // Если его нет и параметры не заданы иначе — упадём ниже с понятным сообщением.
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.local.json", optional: true) // Локальные переопределения, не в репозитории
            .AddEnvironmentVariables()
            .Build();
}
