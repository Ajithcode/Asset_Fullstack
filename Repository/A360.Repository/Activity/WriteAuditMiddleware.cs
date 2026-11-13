using System.Text.Json;
using A360.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace A360.Repository.Activity;

public static class WriteAuditExtensions
{
    /// <summary>
    /// Every successful create / update / delete is recorded in the System Audit Log with who did it, even when the
    /// endpoint doesn't log an event itself. Call it right after UseA360AccessGuard().
    /// </summary>
    public static IApplicationBuilder UseA360WriteAudit(this IApplicationBuilder app) =>
        app.UseMiddleware<WriteAuditMiddleware>();
}

/// <summary>
/// Safety net for the System Audit Log. After a POST / PUT / PATCH / DELETE under /api succeeds (2xx), and the endpoint
/// did not write an event of its own, logs one: Created / Updated / Deleted on the record, by the signed-in user, with
/// IP and browser. The record type comes from the route ("/api/asset-audits/{id}/criteria" → AssetAudit), its id from
/// the route, the Location header or the JSON response's "id", and its name from the response's "name" when present.
/// Read-only POSTs (search, preview, validate, export, …) are skipped.
/// </summary>
public sealed class WriteAuditMiddleware(RequestDelegate next, ILogger<WriteAuditMiddleware> logger)
{
    /// <summary>Set in HttpContext.Items by <see cref="EventLogger"/> when the endpoint logged its own event.</summary>
    public const string EventLoggedKey = "A360.EventLogged";

    /// <summary>Only the start of the response is kept, to read the new record's id and name.</summary>
    private const int CaptureLimit = 64 * 1024;

    /// <summary>POSTs ending in one of these only read or check data; same list as the access guard, plus a few.</summary>
    private static readonly string[] ReadOnlyPostSuffixes =
        ["search", "query", "filter", "preview", "export", "validate", "count", "lookup", "compare", "check", "calculate", "test"];

    /// <summary>Route prefixes that group endpoints but are not record types.</summary>
    private static readonly HashSet<string> NamespaceSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "v1", "v2", "master-data", "administration"
    };

    /// <summary>
    /// Sign-in has its own, more specific events; device telemetry readings are machine-sent and high-volume, so they
    /// are kept out of the audit log on purpose.
    /// </summary>
    private static readonly string[] SkippedPrefixes = ["/api/auth", "/api/telemetry-readings"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsAuditedWrite(context.Request))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        var capture = new CaptureStream(originalBody, CaptureLimit);
        context.Response.Body = capture;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        if (context.Items.ContainsKey(EventLoggedKey)
            || context.Response.StatusCode is < 200 or >= 300
            || context.RequestServices.GetService<IEventLogger>() is not { } eventLogger)
        {
            return;
        }

        try
        {
            var (entityType, entityId, entityName, action, details) = Describe(context, capture);
            await eventLogger.LogDetailedAsync(entityType, entityId, entityName, action, details, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The change is already saved; a missing audit entry must not turn it into an error
            logger.LogWarning(exception, "Could not write the audit event for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
    }

    private static bool IsAuditedWrite(HttpRequest request)
    {
        var method = request.Method;
        if (!(HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
            || !request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || SkippedPrefixes.Any(prefix => request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (HttpMethods.IsPost(method))
        {
            var last = (request.Path.Value ?? string.Empty).TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty;
            return !ReadOnlyPostSuffixes.Any(suffix =>
                last.Equals(suffix, StringComparison.OrdinalIgnoreCase) || last.EndsWith("-" + suffix, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    private static (string EntityType, string EntityId, string EntityName, string Action, string Details) Describe(
        HttpContext context,
        CaptureStream capture)
    {
        var request = context.Request;
        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern;
        var segments = SegmentsOf(pattern, request.Path);

        // Record type: the last literal followed by a {parameter}, else the first literal ("/api/assets/bulk-delete")
        var meaningful = segments.Where(segment => segment.IsParameter || !NamespaceSegments.Contains(segment.Text)).ToList();
        var entityIndex = -1;
        for (var i = 0; i < meaningful.Count - 1; i++)
        {
            if (!meaningful[i].IsParameter && meaningful[i + 1].IsParameter)
            {
                entityIndex = i;
            }
        }

        if (entityIndex < 0)
        {
            entityIndex = meaningful.FindIndex(segment => !segment.IsParameter);
        }

        // POST to a collection under a parent ("/api/devices/{deviceId}/draft/alert-rules") creates that record
        var parentDetail = string.Empty;
        if (HttpMethods.IsPost(request.Method) && meaningful.Count > 1
            && meaningful[^1] is { IsParameter: false } last
            && last.Text.EndsWith('s') && !last.Text.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            && entityIndex >= 0 && entityIndex < meaningful.Count - 1)
        {
            var parent = meaningful[entityIndex];
            var parentId = entityIndex + 1 < meaningful.Count && meaningful[entityIndex + 1].IsParameter
                && request.RouteValues.TryGetValue(meaningful[entityIndex + 1].Text, out var parentValue)
                    ? parentValue?.ToString()
                    : null;
            parentDetail = $"{AuditClassifier.Humanize(ToEntityType(parent.Text))} {parentId}".Trim() + ": ";
            entityIndex = meaningful.Count - 1;
        }

        var entityType = entityIndex >= 0 ? ToEntityType(meaningful[entityIndex].Text) : "Record";
        var trailing = entityIndex >= 0
            ? meaningful.Skip(entityIndex + 1).Where(segment => !segment.IsParameter).Select(segment => segment.Text).ToList()
            : [];

        // Record id: the route value after the record type, else Location header, else the response's "id"
        string? entityId = null;
        if (entityIndex >= 0 && entityIndex + 1 < meaningful.Count && meaningful[entityIndex + 1].IsParameter)
        {
            entityId = request.RouteValues.TryGetValue(meaningful[entityIndex + 1].Text, out var value) ? value?.ToString() : null;
        }

        var (responseId, responseName) = ReadIdAndName(context.Response, capture, entityType);
        entityId ??= LastSegmentOf(context.Response.Headers.Location.ToString()) ?? responseId;

        var method = request.Method;
        var action = HttpMethods.IsDelete(method) || trailing.Any(text => text.Contains("delete", StringComparison.OrdinalIgnoreCase))
            ? EventAction.Deleted
            : HttpMethods.IsPost(method) && trailing.Count == 0 && (entityIndex + 1 >= meaningful.Count)
                ? EventAction.Created
                : EventAction.Updated;

        var routeText = pattern?.RawText is { Length: > 0 } raw ? "/" + raw.TrimStart('/') : request.Path.Value;
        var details = parentDetail + (trailing.Count > 0
            ? $"{string.Join(" / ", trailing.Select(text => AuditClassifier.Humanize(text)))} ({method} {routeText})"
            : $"{method} {routeText}");

        entityId = string.IsNullOrWhiteSpace(entityId) ? "-" : entityId;
        var entityName = string.IsNullOrWhiteSpace(responseName) ? entityId : responseName;
        return (entityType, entityId, entityName, action, details);
    }

    private readonly record struct Segment(string Text, bool IsParameter);

    private static List<Segment> SegmentsOf(RoutePattern? pattern, PathString path)
    {
        if (pattern is null)
        {
            return (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(text => new Segment(text, false))
                .ToList();
        }

        return pattern.PathSegments
            .Select(segment => segment.Parts.FirstOrDefault() switch
            {
                RoutePatternParameterPart parameter => new Segment(parameter.Name, true),
                RoutePatternLiteralPart literal => new Segment(literal.Content, false),
                _ => new Segment(string.Empty, false)
            })
            .Where(segment => segment.Text.Length > 0)
            .ToList();
    }

    /// <summary>"asset-audits" → "AssetAudit", "categories" → "Category", "statuses" → "Status".</summary>
    private static string ToEntityType(string segment)
    {
        var words = segment.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count == 0)
        {
            return "Record";
        }

        words[^1] = Singular(words[^1]);
        return string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

        static string Singular(string word) => word switch
        {
            _ when word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 3 => word[..^3] + "y",
            _ when word.EndsWith("sses", StringComparison.OrdinalIgnoreCase)
                || word.EndsWith("uses", StringComparison.OrdinalIgnoreCase)
                || word.EndsWith("xes", StringComparison.OrdinalIgnoreCase)
                || word.EndsWith("ches", StringComparison.OrdinalIgnoreCase)
                || word.EndsWith("shes", StringComparison.OrdinalIgnoreCase) => word[..^2],
            _ when word.EndsWith('s') && !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) && word.Length > 1 => word[..^1],
            _ => word
        };
    }

    private static string? LastSegmentOf(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var path = Uri.TryCreate(location, UriKind.Absolute, out var uri) ? uri.AbsolutePath : location.Split('?')[0];
        var last = path.TrimEnd('/').Split('/').LastOrDefault();
        return string.IsNullOrWhiteSpace(last) ? null : Uri.UnescapeDataString(last);
    }

    /// <summary>The new / changed record's id and name from a JSON response ({ "id", "name" } or { "data": { … } }).</summary>
    private static (string? Id, string? Name) ReadIdAndName(HttpResponse response, CaptureStream capture, string entityType)
    {
        if (capture.Captured.Length == 0 || capture.Truncated
            || response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(capture.Captured);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            foreach (var candidate in new[] { root }.Concat(NestedObjects(root)))
            {
                var id = Text(candidate, "id") ?? Text(candidate, entityType + "Id") ?? Text(candidate, "code") ?? Text(candidate, "number");
                var name = Text(candidate, "name") ?? Text(candidate, entityType + "Name") ?? Text(candidate, "title")
                    ?? Text(candidate, "displayName") ?? Text(candidate, "label") ?? AnyName(candidate);
                if (id is not null || name is not null)
                {
                    return (id, name);
                }
            }
        }
        catch (JsonException)
        {
        }

        return (null, null);

        static IEnumerable<JsonElement> NestedObjects(JsonElement root)
        {
            foreach (var key in new[] { "data", "item", "result", "record" })
            {
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        yield return property.Value;
                    }
                }
            }
        }

        // First "...Name" property that names the record, not a person or owner ("reportName", "assetName")
        static string? AnyName(JsonElement element)
        {
            string[] notRecordNames = ["userName", "createdByName", "updatedByName", "clientName", "tenantName", "fileName", "ownerName", "assignedToName"];
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.EndsWith("Name", StringComparison.OrdinalIgnoreCase)
                    && !notRecordNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    return property.Value.GetString();
                }
            }

            return null;
        }

        static string? Text(JsonElement element, string name)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.ValueKind switch
                    {
                        JsonValueKind.String when !string.IsNullOrWhiteSpace(property.Value.GetString()) => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetRawText(),
                        _ => null
                    };
                }
            }

            return null;
        }
    }

    /// <summary>Passes the response through unchanged and keeps a copy of its first bytes.</summary>
    private sealed class CaptureStream(Stream inner, int limit) : Stream
    {
        private readonly MemoryStream _copy = new();

        public ReadOnlyMemory<byte> Captured => _copy.GetBuffer().AsMemory(0, (int)_copy.Length);
        public bool Truncated { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Keep(buffer.AsSpan(offset, count));
            inner.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Keep(buffer.AsSpan(offset, count));
            return inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Keep(buffer.Span);
            return inner.WriteAsync(buffer, cancellationToken);
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        private void Keep(ReadOnlySpan<byte> bytes)
        {
            var room = limit - (int)_copy.Length;
            if (bytes.Length > room)
            {
                Truncated = true;
            }

            if (room > 0)
            {
                _copy.Write(bytes[..Math.Min(room, bytes.Length)]);
            }
        }
    }
}
