namespace A360.Media.Client;

public interface IMediaStorageClient
{
    Task<string> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        string category,
        CancellationToken cancellationToken = default);
}
