using System.Reflection;
using A360.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace A360.Repository.Activity;

/// <summary>
/// Adds one System Audit Log event (Imports/Exports tab) per import or export call, without touching the handler:
/// <c>group.MapGet("/export", ExportAsync).LogsExport("StandardReport", "Standard reports");</c>
/// </summary>
public static class ImportExportAuditExtensions
{
    /// <summary>
    /// Logs <see cref="EventAction.Exported"/> when the endpoint returns a file or another success. The record is the
    /// downloaded file's name; details say what was exported.
    /// </summary>
    public static RouteHandlerBuilder LogsExport(this RouteHandlerBuilder builder, string entityType, string description)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            if (!IsSuccess(result))
            {
                return result;
            }

            var fileName = (result as IFileHttpResult)?.FileDownloadName;
            var record = string.IsNullOrWhiteSpace(fileName) ? description : fileName;
            var details = result is FileContentHttpResult { FileLength: { } length }
                ? $"{description} ({FormatSize(length)})"
                : description;

            await LogAsync(context.HttpContext, entityType, record, EventAction.Exported, details);
            return result;
        });
    }

    /// <summary>
    /// Logs <see cref="EventAction.Imported"/> when the endpoint succeeds, or <see cref="EventAction.ImportFailed"/>
    /// when it rejects the file (400 / 409 / 422). The record is the uploaded file's name; details carry the counts the
    /// endpoint returned (created, updated, failed, …).
    /// </summary>
    public static RouteHandlerBuilder LogsImport(this RouteHandlerBuilder builder, string entityType, string description)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            var statusCode = StatusCodeOf(result);
            var succeeded = statusCode is >= 200 and < 300;
            if (!succeeded && statusCode is not (400 or 409 or 422))
            {
                return result;
            }

            var request = context.HttpContext.Request;
            var fileName = request.HasFormContentType && request.Form.Files.Count > 0 ? request.Form.Files[0].FileName : null;
            var record = string.IsNullOrWhiteSpace(fileName) ? description : fileName;

            var counts = succeeded ? CountsOf((result as IValueHttpResult)?.Value) : null;
            var details = succeeded
                ? string.IsNullOrEmpty(counts) ? description : $"{description}: {counts}"
                : $"{description}: rejected (HTTP {statusCode})";

            await LogAsync(context.HttpContext, entityType, record, succeeded ? EventAction.Imported : EventAction.ImportFailed, details);
            return result;
        });
    }

    private static async Task LogAsync(HttpContext httpContext, string entityType, string record, string action, string details)
    {
        var eventLogger = httpContext.RequestServices.GetService<IEventLogger>();
        if (eventLogger is null)
        {
            return;
        }

        // The audit entry must never turn a finished import / export into an error
        try
        {
            await eventLogger.LogDetailedAsync(entityType, record, record, action, details, httpContext.RequestAborted);
        }
        catch (Exception) when (!httpContext.RequestAborted.IsCancellationRequested)
        {
        }
    }

    private static bool IsSuccess(object? result) => StatusCodeOf(result) is >= 200 and < 300;

    /// <summary>Results.File and Results.Stream carry no status code; they are 200.</summary>
    private static int? StatusCodeOf(object? result) => result switch
    {
        IStatusCodeHttpResult { StatusCode: { } code } => code,
        IFileHttpResult => StatusCodes.Status200OK,
        IResult => null,
        null => null,
        _ => StatusCodes.Status200OK // a plain object is serialized as 200
    };

    /// <summary>"created 3, updated 1, failed 0" from the response's whole-number properties.</summary>
    private static string? CountsOf(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var parts = value.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(int) || property.PropertyType == typeof(long))
            .Select(property => $"{AuditClassifier.Humanize(property.Name).ToLowerInvariant()} {property.GetValue(value)}")
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB"
    };
}
