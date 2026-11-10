namespace A360.Media.Api.Services;

public sealed class MediaFileStorageService : IMediaFileStorageService
{
    // .svg, .dwg and .dxf are floor-plan formats (Administration › Maps & Floor Plans)
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".svg", ".dwg", ".dxf", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"];

    private readonly string _uploadRootFolder;
    private readonly string _baseUrl;

    public MediaFileStorageService(IConfiguration configuration)
    {
        _uploadRootFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _baseUrl = (configuration["BaseUrl"] ?? "http://172.16.100.26:5300").TrimEnd('/');
    }

    public async Task<string> SaveFileAsync(IFormFile file, string category, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only jpg, jpeg, png, webp, gif, bmp, svg, dwg, dxf, pdf, doc, docx, xls, xlsx, csv and txt files are allowed.");
        }

        var folder = Path.Combine(_uploadRootFolder, category);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(folder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        return $"{_baseUrl}/uploads/{category}/{fileName}";
    }
}
