using Corvette.MeteoExport.Notifier.Consumers;
using Corvette.MeteoExport.Notifier.Services;
using Corvette.MeteoExport.Notifier.Settings;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace Corvette.MeteoExport.Notifier;

/// <summary>
/// Сервис уведомлений: рассылает письма и вебхуки о завершении заданий.
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

        var settings = builder.Configuration.Get<NotifierSettings>() ?? throw new InvalidOperationException("Конфигурация пуста — рядом с приложением нет appsettings.json.");
        settings.Validate();

        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(settings.Rabbit);

        builder.Services.AddSingleton<EmailSender>();
        builder.Services.AddSingleton<WebhookSender>();

        builder.Services.AddMassTransit(bus =>
        {
            // Два потребителя одного события: каждый со своей очередью, своими повторами и своей
            // очередью ошибок.
            bus.AddConsumer<EmailNotificationConsumer>(typeof(EmailNotificationConsumerDefinition));
            bus.AddConsumer<WebhookNotificationConsumer>(typeof(WebhookNotificationConsumerDefinition));

            bus.UsingRabbitMq((registrationContext, rabbit) =>
            {
                rabbit.Host(settings.Rabbit.HostName, (ushort)settings.Rabbit.Port, settings.Rabbit.VirtualHost, settings.Rabbit.ClientName, host =>
                {
                    host.Username(settings.Rabbit.UserName);
                    host.Password(settings.Rabbit.Password);
                });

                // очереди потребителей — по их определениям
                rabbit.ConfigureEndpoints(registrationContext);
            });
        });

        return builder.Build();
    }
}
