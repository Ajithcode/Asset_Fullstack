using System.Text.Json;
using System.Text.Json.Nodes;

namespace A360.ApiGateway.Swagger;

public sealed record ServiceSwaggerSource(string Name, string RoutePrefix, string DestinationAddress);

public static class SwaggerAggregator
{
    // Discovers every downstream service from the existing ReverseProxy config instead of
    // duplicating the list: any route whose key ends with "-swagger" points at a service
    // whose own swagger.json should be folded into the merged document.
    public static List<ServiceSwaggerSource> GetSources(IConfiguration configuration)
    {
        var sources = new List<ServiceSwaggerSource>();
        var routes = configuration.GetSection("ReverseProxy:Routes");
        var clusters = configuration.GetSection("ReverseProxy:Clusters");

        foreach (var route in routes.GetChildren())
        {
            if (!route.Key.EndsWith("-swagger", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var clusterId = route["ClusterId"];
            var prefix = route["Transforms:0:PathRemovePrefix"];
            if (clusterId is null || prefix is null)
            {
                continue;
            }

            var destination = clusters
                .GetSection($"{clusterId}:Destinations")
                .GetChildren()
                .Select(d => d["Address"])
                .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));

            if (destination is null)
            {
                continue;
            }

            var words = prefix.Trim('/').Split('-', StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(w => w.EndsWith("management", StringComparison.OrdinalIgnoreCase) && w.Length > "management".Length
                    ? new[] { w[..^"management".Length], "management" }
                    : new[] { w });

            var name = string.Join(" ", words.Select(w => w.ToUpperInvariant() == "OT" ? "OT" : char.ToUpperInvariant(w[0]) + w[1..]));

            sources.Add(new ServiceSwaggerSource(name, prefix.Trim('/'), destination.TrimEnd('/')));
        }

        return sources;
    }

    public static async Task<JsonObject> BuildMergedDocumentAsync(
        IEnumerable<ServiceSwaggerSource> sources,
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var merged = new JsonObject
        {
            ["openapi"] = "3.0.1",
            ["info"] = new JsonObject
            {
                ["title"] = "A360 - All Services (Aggregated)",
                ["version"] = "v1",
            },
            ["paths"] = new JsonObject(),
            ["components"] = new JsonObject
            {
                ["schemas"] = new JsonObject(),
                ["securitySchemes"] = new JsonObject(),
            },
            ["tags"] = new JsonArray(),
        };

        var mergedPaths = (JsonObject)merged["paths"]!;
        var mergedSchemas = (JsonObject)merged["components"]!["schemas"]!;
        var mergedSecuritySchemes = (JsonObject)merged["components"]!["securitySchemes"]!;
        var mergedTags = (JsonArray)merged["tags"]!;
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var client = httpClientFactory.CreateClient("SwaggerAggregator");

        foreach (var source in sources)
        {
            string raw;
            try
            {
                raw = await client.GetStringAsync($"{source.DestinationAddress}/swagger/v1/swagger.json", cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping {Service} swagger — could not reach {Address}", source.Name, source.DestinationAddress);
                continue;
            }

            var servicePrefix = source.Name.Replace(" ", string.Empty);

            // Rename every schema ref up front (exact quoted-string match keeps "User" from
            // colliding with "UserRole") so paths and components can be merged without clashes.
            JsonObject doc;
            try
            {
                var schemaNames = JsonNode.Parse(raw)?["components"]?["schemas"]?.AsObject()
                    .Select(kv => kv.Key)
                    .OrderByDescending(n => n.Length)
                    .ToList() ?? new List<string>();

                foreach (var schemaName in schemaNames)
                {
                    raw = raw.Replace(
                        $"\"#/components/schemas/{schemaName}\"",
                        $"\"#/components/schemas/{servicePrefix}_{schemaName}\"");
                }

                doc = JsonNode.Parse(raw)!.AsObject();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping {Service} swagger — invalid document", source.Name);
                continue;
            }

            if (!seenTags.Contains(source.Name))
            {
                seenTags.Add(source.Name);
                mergedTags.Add(new JsonObject { ["name"] = source.Name });
            }

            if (doc["paths"] is JsonObject paths)
            {
                foreach (var (path, item) in paths.ToList())
                {
                    paths.Remove(path);
                    if (item is JsonObject pathItem)
                    {
                        TagOperations(pathItem, source.Name, servicePrefix);
                    }

                    var gatewayPath = $"/{source.RoutePrefix}{path}";
                    mergedPaths[gatewayPath] = item;
                }
            }

            if (doc["components"]?["schemas"] is JsonObject schemas)
            {
                foreach (var (schemaName, schema) in schemas.ToList())
                {
                    schemas.Remove(schemaName);
                    mergedSchemas[$"{servicePrefix}_{schemaName}"] = schema;
                }
            }

            if (doc["components"]?["securitySchemes"] is JsonObject securitySchemes)
            {
                foreach (var (schemeName, scheme) in securitySchemes.ToList())
                {
                    securitySchemes.Remove(schemeName);
                    var key = mergedSecuritySchemes.ContainsKey(schemeName) ? $"{servicePrefix}_{schemeName}" : schemeName;
                    mergedSecuritySchemes[key] = scheme;
                }
            }
        }

        return merged;
    }

    private static void TagOperations(JsonObject pathItem, string serviceName, string servicePrefix)
    {
        var httpVerbs = new[] { "get", "post", "put", "delete", "patch", "head", "options", "trace" };

        foreach (var verb in httpVerbs)
        {
            if (pathItem[verb] is not JsonObject operation)
            {
                continue;
            }

            operation["tags"] = new JsonArray(JsonValue.Create(serviceName));

            if (operation["operationId"] is JsonValue existingId && existingId.TryGetValue<string>(out var id))
            {
                operation["operationId"] = $"{servicePrefix}_{id}";
            }
        }
    }
}
