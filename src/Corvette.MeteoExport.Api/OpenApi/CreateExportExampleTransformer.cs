using System.Text.Json.Nodes;
using Corvette.MeteoExport.Api.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corvette.MeteoExport.Api.OpenApi;

/// <summary>
/// Кладёт в описание POST /exports готовые тела запроса.
/// </summary>
public class CreateExportExampleTransformer : IOpenApiOperationTransformer
{
    private const string RelativePath = "exports";
    private const string HttpMethod = "POST";
    private const string MediaType = "application/json";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Description.RelativePath != RelativePath) return Task.CompletedTask;
        if (context.Description.HttpMethod != HttpMethod) return Task.CompletedTask;

        var content = operation.RequestBody?.Content;
        if (content == null || !content.TryGetValue(MediaType, out var mediaType))
            return Task.CompletedTask;

        mediaType.Examples = new Dictionary<string, IOpenApiExample>
        {
            ["minimal"] = new OpenApiExample
            {
                Summary = "Минимальный запрос",
                Value = BuildMinimalExample(),
            },
            ["full"] = new OpenApiExample
            {
                Summary = "Запрос со всеми полями",
                Value = BuildFullExample(),
            },
        };

        // Спецификация не разрешает example и examples одновременно.
        mediaType.Example = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Самый короткий запрос, который проходит валидацию.
    /// </summary>
    private static JsonObject BuildMinimalExample() =>
        new()
        {
            ["points"] = new JsonArray
            {
                new JsonObject
                {
                    ["latitude"] = 59.94,
                    ["longitude"] = 30.31,
                    ["name"] = "Санкт-Петербург",
                },
            },
            ["from"] = "1990-01-01",
            ["to"] = "1990-12-31",
        };

    /// <summary>
    /// Тот же запрос со всеми полями: величины целиком, необязательные адреса с null.
    /// </summary>
    private static JsonObject BuildFullExample()
    {
        var example = BuildMinimalExample();

        // Из того же массива, что и валидация с перечислением в схеме: разъехаться не может.
        example["variables"] = new JsonArray([.. CreateExportRequest.AllowedVariables.Select(x => (JsonNode)JsonValue.Create(x))]);
        example["email"] = null;
        example["webhookUrl"] = null;

        return example;
    }
}
