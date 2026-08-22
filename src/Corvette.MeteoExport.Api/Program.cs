using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.S3;
using Corvette.MeteoExport.Api.Middleware;
using Corvette.MeteoExport.Api.OpenApi;
using Corvette.MeteoExport.Api.Settings;
using Corvette.MeteoExport.Core;
using MassTransit;
using NLog;
using NLog.Extensions.Logging;
using Scalar.AspNetCore;

namespace Corvette.MeteoExport.Api;

/// <summary>
/// Сервис приёма запросов на выгрузку: принимает их, заводит задания и отдаёт их состояние.
/// </summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitConfigurationError = 2;

    private const string NLogConfigFileName = "NLog.config";
    private static readonly TimeSpan OutboxQueryDelay = TimeSpan.FromSeconds(1);

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
            WebApplication application;
            try
            {
                application = BuildApplication(args);
            }
            catch (Exception exception)
            {
                logger.Fatal(exception, "Не удалось прочитать конфигурацию.");
                return ExitConfigurationError;
            }

            await application.RunAsync();
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

    private static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
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

        var settings = builder.Configuration.Get<ApiSettings>() ?? throw new InvalidOperationException("Конфигурация пуста — рядом с приложением нет appsettings.json.");
        settings.Validate();

        // Вложенные секции кладём в контейнер отдельно, чтобы сервисы просили ровно то, что им нужно.
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(settings.Rabbit);
        builder.Services.AddSingleton(settings.Storage);

        // Клиент хранилища держит соединения, поэтому он один на приложение.
        builder.Services.AddSingleton(_ => new AmazonS3Client(
            new BasicAWSCredentials(settings.Storage.AccessKey, settings.Storage.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.Storage.Endpoint,
                ForcePathStyle = true,
            }));

        // Контекст на запрос ради MassTransit.Outbox
        builder.Services.AddDbContext<MeteoExportDbContext>(options => MeteoExportDbContextFactory.Configure(options, settings.ConnectionString));
        
        builder.Services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<MeteoExportDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay = OutboxQueryDelay; // Как часто доставщик заглядывает в таблицу исходящих
            });

            bus.UsingRabbitMq((_, rabbit) =>
            {
                rabbit.Host(settings.Rabbit.HostName, (ushort)settings.Rabbit.Port, settings.Rabbit.VirtualHost, settings.Rabbit.ClientName, host =>
                {
                    host.Username(settings.Rabbit.UserName);
                    host.Password(settings.Rabbit.Password);
                });
            });
        });

        builder.Services
            .AddControllers()
            // Статус в ответе — строкой: наружу уходит контракт, а не номер значения в enum.
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<CreateExportExampleTransformer>();
            options.AddSchemaTransformer<AllowedVariablesTransformer>();
        });

        var application = builder.Build();

        // Первым в конвейере: ловит и то, что бросили middleware ниже.
        application.UseMiddleware<ErrorHandlingMiddleware>();

        application.MapOpenApi();
        application.MapScalarApiReference();
        application.MapControllers();

        return application;
    }
}
