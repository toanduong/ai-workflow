using System.Net;
using Microsoft.Azure.Cosmos;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Persistence.Cosmos.Repositories;

public sealed class CosmosExecutionRepository(CosmosDbContext context) : IExecutionRepository
{
    public async Task<WorkflowExecution?> GetByIdAsync(ExecutionId id, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.Value.ToString());

        var iterator = context.Executions.GetItemQueryIterator<WorkflowExecution>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var result = response.FirstOrDefault();
            if (result is not null) return result;
        }

        return null;
    }

    public async Task<IReadOnlyList<WorkflowExecution>> GetByWorkflowIdAsync(WorkflowId workflowId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.workflowId = @workflowId")
            .WithParameter("@workflowId", workflowId.Value.ToString());

        var iterator = context.Executions.GetItemQueryIterator<WorkflowExecution>(query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(workflowId.Value.ToString()) });

        var results = new List<WorkflowExecution>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results.AsReadOnly();
    }

    public async Task AddAsync(WorkflowExecution execution, CancellationToken cancellationToken = default)
    {
        await context.Executions.CreateItemAsync(
            execution,
            new PartitionKey(execution.WorkflowId.Value.ToString()),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(WorkflowExecution execution, CancellationToken cancellationToken = default)
    {
        await context.Executions.UpsertItemAsync(
            execution,
            new PartitionKey(execution.WorkflowId.Value.ToString()),
            cancellationToken: cancellationToken);
    }
}
