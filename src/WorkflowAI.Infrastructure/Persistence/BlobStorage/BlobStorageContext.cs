using System.Text.Json;
using Azure.Storage.Blobs;

namespace WorkflowAI.Infrastructure.Persistence.BlobStorage;

public sealed class BlobStorageContext
{
    public BlobContainerClient Workflows { get; }
    public BlobContainerClient Executions { get; }
    public BlobContainerClient Approvals { get; }
    public BlobContainerClient Notifications { get; }
    public BlobContainerClient AITasks { get; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public BlobStorageContext(BlobServiceClient blobServiceClient)
    {
        Workflows = blobServiceClient.GetBlobContainerClient(BlobContainerNames.Workflows);
        Executions = blobServiceClient.GetBlobContainerClient(BlobContainerNames.Executions);
        Approvals = blobServiceClient.GetBlobContainerClient(BlobContainerNames.Approvals);
        Notifications = blobServiceClient.GetBlobContainerClient(BlobContainerNames.Notifications);
        AITasks = blobServiceClient.GetBlobContainerClient(BlobContainerNames.AITasks);

        // Containers are created lazily on first use — no async in constructor.
        // Call EnsureContainersExistAsync() at app startup if needed.
    }

    public async Task EnsureContainersExistAsync(CancellationToken ct = default)
    {
        await Workflows.CreateIfNotExistsAsync(cancellationToken: ct);
        await Executions.CreateIfNotExistsAsync(cancellationToken: ct);
        await Approvals.CreateIfNotExistsAsync(cancellationToken: ct);
        await Notifications.CreateIfNotExistsAsync(cancellationToken: ct);
        await AITasks.CreateIfNotExistsAsync(cancellationToken: ct);
    }
}
