using System.Net;
using System.Net.Http.Json;

namespace A360.Media.Client;

internal sealed record MediaUploadResponse(string Url);

internal sealed record MediaErrorResponse(string Message);

public sealed class MediaStorageClient : IMediaStorageClient
{
    private readonly HttpClient _httpClient;

    public MediaStorageClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        string category,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        form.Add(streamContent, "file", fileName);
        form.Add(new StringContent(category), "category");

        using var response = await _httpClient.PostAsync("/api/media/upload", form, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadFromJsonAsync<MediaErrorResponse>(cancellationToken: cancellationToken);
            throw new InvalidOperationException(error?.Message ?? "The file could not be uploaded.");
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MediaUploadResponse>(cancellationToken: cancellationToken);

        if (result is null || string.IsNullOrWhiteSpace(result.Url))
        {
            throw new InvalidOperationException("The media service returned an empty upload URL.");
        }

        return result.Url;
    }
}
