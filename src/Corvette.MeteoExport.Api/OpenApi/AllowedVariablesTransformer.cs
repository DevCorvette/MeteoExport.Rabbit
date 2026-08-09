using System.Text.Json.Nodes;
using Corvette.MeteoExport.Api.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corvette.MeteoExport.Api.OpenApi;

/// <summary>
/// Перечисляет в схеме запроса разрешённые погодные величины.
/// </summary>
public class AllowedVariablesTransformer : IOpenApiSchemaTransformer
{
    private const string PropertyName = "variables";

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (context.JsonTypeInfo.Type != typeof(CreateExportRequest)) return Task.CompletedTask;
        if (schema.Properties == null || !schema.Properties.TryGetValue(PropertyName, out var variables)) return Task.CompletedTask;

        // Перечисление вешаем на элемент массива: ограничен каждый элемент, а не список целиком.
        var itemSchema = (variables as OpenApiSchema)?.Items as OpenApiSchema;
        if (itemSchema == null)
            return Task.CompletedTask;

        itemSchema.Enum = [.. CreateExportRequest.AllowedVariables.Select(x => (JsonNode)JsonValue.Create(x))];

        return Task.CompletedTask;
    }
}
