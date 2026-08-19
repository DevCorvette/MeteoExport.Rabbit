using System.Diagnostics;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;

namespace Corvette.MeteoExport.Api.Middleware;

/// <summary>
/// Ловит всё, что не поймали ниже: пишет в лог и отвечает клиенту без подробностей.
/// </summary>
public class ErrorHandlingMiddleware
{
    /// <summary>
    /// Клиент оборвал запрос.
    /// </summary>
    private const int StatusClientClosedRequest = 499;

    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        try
        {
            await _next(httpContext);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // Клиент ушёл — это норма жизни, а не отказ сервиса.
            _logger.LogWarning($"Клиент оборвал запрос (Method=\"{httpContext.Request.Method}\", Path=\"{httpContext.Request.Path}\")");
            httpContext.Response.StatusCode = StatusClientClosedRequest;
        }
        catch (BadHttpRequestException exception)
        {
            _logger.LogWarning(exception, $"Запрос не разобран (Method=\"{httpContext.Request.Method}\", Path=\"{httpContext.Request.Path}\")");
            await WriteProblemAsync(httpContext, StatusCodes.Status400BadRequest, "Некорректный запрос", "Запрос не разобран.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Необработанная ошибка (Method=\"{httpContext.Request.Method}\", Path=\"{httpContext.Request.Path}\", TraceId=\"{GetTraceId(httpContext)}\")");
            await WriteProblemAsync(httpContext, StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервиса", "Запрос не обработан. Попробуйте позже.");
        }
    }

    /// <summary>
    /// Отвечает клиенту в том же формате, в котором отвечает на негодный запрос сам ASP.NET Core.
    /// </summary>
    private async Task WriteProblemAsync(HttpContext httpContext, int statusCode, string title, string detail)
    {
        if (httpContext.Response.HasStarted)
        {
            // Заголовки и часть тела уже у клиента, дописать к ним ответ другого формата нельзя.
            _logger.LogWarning($"Ответ уже начат, тело ошибки не отправлено (TraceId=\"{GetTraceId(httpContext)}\")");
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
        };

        problem.Extensions["traceId"] = GetTraceId(httpContext);
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = MediaTypeNames.Application.ProblemJson;

        await httpContext.Response.WriteAsJsonAsync(problem);
    }

    private static string GetTraceId(HttpContext httpContext) => Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
