using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZahSellerAI.Application.Interfaces;

namespace ZahSellerAI.Infrastructure.Storage;

public class ImageStorageService : IImageStorageService
{
    private readonly string _uploadBasePath;
    private readonly string _baseUrl;
    private readonly ILogger<ImageStorageService> _logger;

    public ImageStorageService(IConfiguration configuration, ILogger<ImageStorageService> logger)
    {
        _logger = logger;
        _baseUrl = configuration["Storage:LocalBaseUrl"] ?? "http://localhost:5000";
        _uploadBasePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        if (!Directory.Exists(_uploadBasePath))
        {
            Directory.CreateDirectory(_uploadBasePath);
        }
    }

    public async Task<string> UploadAsync(
        Stream imageStream,
        string fileName,
        string folderPath,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetFolder = Path.Combine(_uploadBasePath, folderPath);
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
            var filePath = Path.Combine(targetFolder, safeFileName);

            using (var destinationStream = new FileStream(filePath, FileMode.Create))
            {
                await imageStream.CopyToAsync(destinationStream, cancellationToken);
            }

            var relativePath = Path.Combine("uploads", folderPath, safeFileName).Replace("\\", "/");
            var fullUrl = $"{_baseUrl.TrimEnd('/')}/{relativePath}";

            _logger.LogInformation("Image stored successfully at {FilePath}, Public URL: {PublicUrl}", filePath, fullUrl);
            return fullUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image {FileName} to storage", fileName);
            throw;
        }
    }

    public Task<bool> DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl)) return Task.FromResult(false);

            var uri = new Uri(fileUrl);
            var relativePath = uri.AbsolutePath.TrimStart('/');
            var localPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete image for URL: {FileUrl}", fileUrl);
            return Task.FromResult(false);
        }
    }

    public Task<string> GetUrlAsync(string filePath)
    {
        var relativePath = filePath.Replace("\\", "/").TrimStart('/');
        return Task.FromResult($"{_baseUrl.TrimEnd('/')}/{relativePath}");
    }
}
