namespace ZahSellerAI.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string folderPath,
        string contentType,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAsync(
        string fileUrl,
        CancellationToken cancellationToken = default
    );

    Task<string> GetUrlAsync(
        string filePath
    );
}
