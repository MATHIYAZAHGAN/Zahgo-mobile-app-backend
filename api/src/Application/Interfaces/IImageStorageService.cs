namespace ZahSellerAI.Application.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadAsync(
        Stream imageStream,
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
