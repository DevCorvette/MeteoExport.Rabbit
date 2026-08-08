using Corvette.MeteoExport.Core;
using Corvette.MeteoExport.Migrator.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace Corvette.MeteoExport.Migrator;

/// <summary>
/// Применяет к базе все неприменённые EF-миграции и завершается.
/// </summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitConfigurationError = 2;

    private const string NLogConfigFileName = "NLog.config";

    private static async Task<int> Main()
    {
        // Если NLog.config рядом с приложением нет - упадём
        if (LogManager.Configuration == null)
        {
            await Console.Error.WriteLineAsync($"Рядом с приложением нет {NLogConfigFileName} — логировать некуда.");
            return ExitConfigurationError;
        }

        var logger = LogManager.GetCurrentClassLogger();

        try
        {
            logger.Info("Мигратор запущен.");

            MigratorSettings migratorSettings;
            try
            {
                migratorSettings = MigratorSettings.Load();
            }
            catch (Exception exception)
            {
                logger.Fatal(exception, "Не удалось прочитать конфигурацию.");
                return ExitConfigurationError;
            }

            return await MigrateAsync(migratorSettings, logger);
        }
        catch (Exception exception)
        {
            logger.Fatal(exception, "Ошибка при выполнении миграций.");
            return ExitFailure;
        }
        finally
        {
            // Дописывает буферы на диск
            LogManager.Shutdown();
        }
    }

    private static async Task<int> MigrateAsync(MigratorSettings migratorSettings, Logger logger)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        var contextFactory = new MeteoExportDbContextFactory(migratorSettings.ConnectionString, loggerFactory);

        await using (var context = contextFactory.CreateDbContext())
        {
            if (await context.Database.CanConnectAsync())
            {
                var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToArray();
                if (pendingMigrations.Length == 0)
                {
                    logger.Info("База актуальна, применять нечего.");
                    return ExitSuccess;
                }

                logger.Info($"Найдены неприменённые миграции (Count={pendingMigrations.Length}, Migrations=\"{string.Join(", ", pendingMigrations)}\")");
            }
            else
            {
                // Отсутствующую базу MigrateAsync создаст сам. Если причина недоступности другая, упадём ниже с текстом ошибки.
                logger.Info("База недоступна, она будет создана, если её нет.");
            }

            await context.Database.MigrateAsync();

            logger.Info("Миграции успешно применены.");
            return ExitSuccess;
        }
    }
}
