using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Corvette.MeteoExport.Core;

/// <summary>
/// Создаёт <see cref="MeteoExportDbContext"/> с настроенными опциями подключения.
/// </summary>
public class MeteoExportDbContextFactory : IDbContextFactory<MeteoExportDbContext>
{
    /// <summary>
    /// Таблица истории применённых миграций
    /// </summary>
    private const string MigrationsHistoryTableName = "__ef_migrations_history";

    private readonly DbContextOptions<MeteoExportDbContext> _options;

    public MeteoExportDbContextFactory(string connectionString, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new DbContextOptionsBuilder<MeteoExportDbContext>();
        Configure(builder, connectionString, loggerFactory);
        _options = builder.Options;
    }

    public MeteoExportDbContext CreateDbContext() => new(_options);

    /// <summary>
    /// Единая точка настройки опций контекста.
    /// </summary>
    public static void Configure(
        DbContextOptionsBuilder builder,
        string connectionString,
        ILoggerFactory? loggerFactory = null)
    {
        var npgsqlConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timezone = "UTC",
        }.ConnectionString;

        builder.UseNpgsql(
            npgsqlConnectionString,
            npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTableName));

        if (loggerFactory != null)
            builder.UseLoggerFactory(loggerFactory);
    }
}
