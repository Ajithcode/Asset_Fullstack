using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;

namespace A360.ApiGateway.Middlewares;

/// <summary>
/// Raises the request body limit to 50 MB (plus multipart overhead) for the file upload routes only:
/// media upload and the asset Upload Images / Upload Document calls. Every other route keeps Kestrel's default (~28.6 MB).
/// </summary>
public sealed partial class UploadSizeLimitMiddleware(RequestDelegate next)
{
    public const long MaxUploadFileBytes = 50L * 1024 * 1024;
    private const long MaxUploadRequestBytes = MaxUploadFileBytes + 1024 * 1024;

    [GeneratedRegex(@"^(/media)?/api/media/upload/?$|^/api/asset-registrations/[^/]+/(images|documents)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex UploadPath();

    public Task InvokeAsync(HttpContext context)
    {
        if ((HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method))
            && UploadPath().IsMatch(context.Request.Path.Value ?? string.Empty))
        {
            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false })
            {
                feature.MaxRequestBodySize = MaxUploadRequestBytes;
            }
        }

        return next(context);
    }
}
