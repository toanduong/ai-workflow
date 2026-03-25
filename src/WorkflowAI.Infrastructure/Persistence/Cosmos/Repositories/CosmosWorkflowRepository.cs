using System.Net;
using Microsoft.Azure.Cosmos;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Persistence.Cosmos.Repositories;

public sealed class CosmosWorkflowRepository(CosmosDbContext context) : IWorkflowRepository
{
    public async Task<Workflow?> GetByIdAsync(WorkflowId id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await context.Workflows.ReadItemAsync<Workflow>(
                id.Value.ToString(),
                new PartitionKey(id.Value.ToString()),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Workflow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var iterator = context.Workflows.GetItemQueryIterator<Workflow>(query);
        var results = new List<Workflow>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results.AsReadOnly();
    }

    public async Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        await context.Workflows.CreateItemAsync(
            workflow,
            new PartitionKey(workflow.Id.Value.ToString()),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        await context.Workflows.UpsertItemAsync(
            workflow,
            new PartitionKey(workflow.Id.Value.ToString()),
            cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(WorkflowId id, CancellationToken cancellationToken = default)
    {
        await context.Workflows.DeleteItemAsync<Workflow>(
            id.Value.ToString(),
            new PartitionKey(id.Value.ToString()),
            cancellationToken: cancellationToken);
    }
}
