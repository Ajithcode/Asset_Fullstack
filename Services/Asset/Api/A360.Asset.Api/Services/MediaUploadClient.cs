using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace A360.Asset.Api.Services;

/// <param name="Url">Where the Media service serves the file; null when the upload failed.</param>
/// <param name="Error">Why the upload failed, for the caller.</param>
public sealed record MediaUploadResult(string? Url, string? Error);

/// <summary>Stores files through the Media service (POST /api/media/upload), which owns uploaded files.</summary>
public interface IMediaUploadClient
{
    /// <param name="category">Folder the file is stored under, e.g. "asset-images".</param>
    Task<MediaUploadResult> UploadAsync(IFormFile file, string category, CancellationToken cancellationToken = default);
}

public sealed class MediaUploadClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : IMediaUploadClient
{
    public const string ClientName = "A360Media";

    public async Task<MediaUploadResult> UploadAsync(IFormFile file, string category, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
        }

        content.Add(fileContent, "file", file.FileName);
        content.Add(new StringContent(category), "category");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/media/upload") { Content = content };

        // The Media service checks the caller's sign-in, so the caller's token is passed on.
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        try
        {
            using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadMessageAsync(response, cancellationToken);
                return new MediaUploadResult(null, $"'{file.FileName}' could not be stored: {message}");
            }

            var body = await response.Content.ReadFromJsonAsync<MediaUploadBody>(cancellationToken);
            return string.IsNullOrWhiteSpace(body?.Url)
                ? new MediaUploadResult(null, $"'{file.FileName}' could not be stored: the Media service returned no URL.")
                : new MediaUploadResult(body.Url, null);
        }
        catch (HttpRequestException)
        {
            return new MediaUploadResult(null, "The file service is not reachable. Try again in a moment.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MediaUploadResult(null, "The file service did not respond in time. Try again in a moment.");
        }
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<MediaErrorBody>(cancellationToken);
            if (!string.IsNullOrWhiteSpace(body?.Message))
            {
                return body.Message;
            }
        }
        catch (Exception)
        {
            // Not JSON; fall back to the status code.
        }

        return $"HTTP {(int)response.StatusCode}";
    }

    private sealed record MediaUploadBody(string? Url);

    private sealed record MediaErrorBody(string? Message);
}
