using System.Text;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Storage;

/// <summary>
/// Local file storage implementation for development purposes.
/// Saves files to project's luu-file-workflow/ folder instead of Azure Blob Storage.
/// </summary>
public sealed class LocalFileStorageService(ILogger<LocalFileStorageService> logger) : IBlobStorageService
{
    private static readonly string BaseDirectory = "/Users/levanlap/Desktop/Cong Viec It/HA/Gotik/BE-api-workflow/ai-workflow/luu-file-workflow";

    public async Task<string> UploadAsync(string containerName, string blobName, string content, CancellationToken cancellationToken = default)
    {
        var containerPath = Path.Combine(BaseDirectory, containerName);
        Directory.CreateDirectory(containerPath);

        var filePath = Path.Combine(containerPath, blobName);
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);

        logger.LogInformation("Saved file {BlobName} to local storage at {FilePath}", blobName, filePath);
        
        // Return file:// URI similar to how blob storage returns a URI
        return new Uri(filePath).AbsoluteUri;
    }

    public async Task<string?> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(BaseDirectory, containerName, blobName);

        if (!File.Exists(filePath))
            return null;

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(BaseDirectory, containerName, blobName);

        if (!File.Exists(filePath))
            return Task.FromResult(false);

        File.Delete(filePath);
        logger.LogInformation("Deleted file {BlobName} from local storage at {FilePath}", blobName, filePath);
        return Task.FromResult(true);
    }
}
