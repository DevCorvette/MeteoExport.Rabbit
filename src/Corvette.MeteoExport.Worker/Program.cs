using Corvette.MeteoExport.Core;
using MassTransit;
using Corvette.MeteoExport.Worker.Consumers;
using Corvette.MeteoExport.Worker.HostedServices;
using Corvette.MeteoExport.Worker.Services;
using Corvette.MeteoExport.Worker.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace Corvette.MeteoExport.Worker;

/// <summary>
/// Сервис выгрузки: разбирает команды из очереди и выполняет задания.
/// </summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitConfigurationError = 2;

    private const string NLogConfigFileName = "NLog.config";

    private static async Task<int> Main(string[] args)
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
            IHost host;
            try
            {
                host = BuildHost(args);
            }
            catch (Exception exception)
            {
                logger.Fatal(exception, "Не удалось прочитать конфигурацию.");
                return ExitConfigurationError;
            }

            await host.RunAsync();
            return ExitSuccess;
        }
        catch (Exception exception)
        {
            logger.Fatal(exception, "Сервис завершился с ошибкой.");
            return ExitFailure;
        }
        finally
        {
            // Дописывает буферы на диск
            LogManager.Shutdown();
        }
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            // Каталог приложения, а не рабочий каталог: appsettings.json лежит рядом с exe.
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);

        builder.Logging.ClearProviders();
        // AddNLog снимает фильтр Microsoft.Extensions.Logging и забирает фильтрацию себе, поэтому
        // уровни настраиваются только в NLog.config
        builder.Logging.AddNLog();

        var settings = builder.Configuration.Get<WorkerSettings>() ?? throw new InvalidOperationException("Конфигурация пуста — рядом с приложением нет appsettings.json.");
        settings.Validate();

        // Вложенные секции кладём в контейнер отдельно, чтобы сервисы просили ровно то, что им нужно.
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(settings.Rabbit);
        builder.Services.AddSingleton(settings.Storage);
        builder.Services.AddSingleton(settings.OpenMeteo);

        builder.Services.AddDbContext<MeteoExportDbContext>(options => MeteoExportDbContextFactory.Configure(options, settings.ConnectionString));

        // scoped для контекста
        builder.Services.AddScoped<ExportJobRepository>();
        builder.Services.AddScoped<ExportBuilder>();
        builder.Services.AddScoped<ExportRunner>();

        builder.Services.AddSingleton<ResultStorage>();
        builder.Services.AddSingleton<DraftFiles>();
        builder.Services.AddSingleton<OpenMeteoClient>();

        // Раньше шины: к приходу первой команды каталог черновиков чист, а корзина заведена.
        builder.Services.AddHostedService<StartupService>();

        builder.Services.AddMassTransit(bus =>
        {
            bus.AddConsumer<ExportConsumer>(typeof(ExportConsumerDefinition));
            bus.AddConsumer<FinishExportConsumer>(typeof(FinishExportConsumerDefinition));
            bus.AddConsumer<ExportFaultConsumer>(typeof(ExportFaultConsumerDefinition));

            // Аутбокс точки приёма для финализатора
            bus.AddEntityFrameworkOutbox<MeteoExportDbContext>(outbox => outbox.UsePostgres());

            bus.UsingRabbitMq((registrationContext, rabbit) =>
            {
                rabbit.Host(settings.Rabbit.HostName, (ushort)settings.Rabbit.Port, settings.Rabbit.VirtualHost, settings.Rabbit.ClientName, host =>
                {
                    host.Username(settings.Rabbit.UserName);
                    host.Password(settings.Rabbit.Password);
                });

                // очередь потребителя — по его определению
                rabbit.ConfigureEndpoints(registrationContext);
            });
        });

        return builder.Build();
    }
}
