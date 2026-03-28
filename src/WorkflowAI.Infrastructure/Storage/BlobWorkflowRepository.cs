using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Storage;

public sealed class BlobWorkflowRepository(BlobServiceClient blobServiceClient) : IWorkflowRepository
{
    private const string ContainerName = "workflows";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new WorkflowIdJsonConverter() }
    };

    public async Task<Workflow?> GetByIdAsync(WorkflowId id, CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerAsync(cancellationToken);
        var blobClient = containerClient.GetBlobClient(ToBlobName(id));

        if (!await blobClient.ExistsAsync(cancellationToken))
            return null;

        var response = await blobClient.DownloadContentAsync(cancellationToken);
        return JsonSerializer.Deserialize<Workflow>(response.Value.Content.ToString(), SerializerOptions);
    }

    public async Task<IReadOnlyList<Workflow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerAsync(cancellationToken);
        var workflows = new List<Workflow>();

        await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            var blobClient = containerClient.GetBlobClient(blobItem.Name);
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var workflow = JsonSerializer.Deserialize<Workflow>(response.Value.Content.ToString(), SerializerOptions);
            if (workflow is not null)
                workflows.Add(workflow);
        }

        return workflows.AsReadOnly();
    }

    public async Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerAsync(cancellationToken);
        var blobClient = containerClient.GetBlobClient(ToBlobName(workflow.Id));

        var json = JsonSerializer.Serialize(workflow, SerializerOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: false, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerAsync(cancellationToken);
        var blobClient = containerClient.GetBlobClient(ToBlobName(workflow.Id));

        var json = JsonSerializer.Serialize(workflow, SerializerOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(WorkflowId id, CancellationToken cancellationToken = default)
    {
        var containerClient = await GetContainerAsync(cancellationToken);
        var blobClient = containerClient.GetBlobClient(ToBlobName(id));
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return containerClient;
    }

    private static string ToBlobName(WorkflowId id) => $"{id.Value}.json";

    private sealed class WorkflowIdJsonConverter : JsonConverter<WorkflowId>
    {
        public override WorkflowId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetGuid());

        public override void Write(Utf8JsonWriter writer, WorkflowId value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }
}
