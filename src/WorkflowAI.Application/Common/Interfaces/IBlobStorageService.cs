namespace WorkflowAI.Application.Common.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string containerName, string blobName, string content, CancellationToken cancellationToken = default);
    Task<string?> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
}
