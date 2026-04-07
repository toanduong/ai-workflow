using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace WorkflowAI.Infrastructure.Persistence.BlobStorage;

internal abstract class BlobRepositoryBase
{
    private readonly BlobContainerClient _container;

    protected BlobRepositoryBase(BlobContainerClient container)
    {
        _container = container;
    }

    protected async Task<T?> GetAsync<T>(Guid id, CancellationToken ct) where T : class
    {
        var blob = _container.GetBlobClient($"{id}.json");
        if (!await blob.ExistsAsync(ct)) return null;
        var response = await blob.DownloadContentAsync(ct);
        return JsonSerializer.Deserialize<T>(response.Value.Content, BlobStorageContext.JsonOptions);
    }

    protected async Task<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken ct) where T : class
    {
        var results = new List<T>();
        await foreach (var item in _container.GetBlobsAsync(cancellationToken: ct))
        {
            var blob = _container.GetBlobClient(item.Name);
            var response = await blob.DownloadContentAsync(ct);
            var entity = JsonSerializer.Deserialize<T>(response.Value.Content, BlobStorageContext.JsonOptions);
            if (entity is not null) results.Add(entity);
        }
        return results.AsReadOnly();
    }

    protected async Task SaveAsync<T>(Guid id, T entity, bool overwrite, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(entity, BlobStorageContext.JsonOptions);
        var blob = _container.GetBlobClient($"{id}.json");
        await blob.UploadAsync(new BinaryData(json), overwrite: overwrite, cancellationToken: ct);
    }

    protected async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var blob = _container.GetBlobClient($"{id}.json");
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }
}
