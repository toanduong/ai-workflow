using Microsoft.Azure.Cosmos;
using WorkflowAI.Domain.AIAgent;

namespace WorkflowAI.Infrastructure.Persistence.Cosmos.Repositories;

public sealed class CosmosAIAgentTaskRepository(CosmosDbContext context) : IAIAgentTaskRepository
{
    public async Task<AIAgentTask?> GetByIdAsync(AIAgentTaskId id, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.Value.ToString());

        var iterator = context.AITasks.GetItemQueryIterator<AIAgentTask>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var result = response.FirstOrDefault();
            if (result is not null) return result;
        }
        return null;
    }

    public async Task<IReadOnlyList<AIAgentTask>> GetByStepExecutionIdAsync(Guid stepExecutionId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.stepExecutionId = @stepId")
            .WithParameter("@stepId", stepExecutionId.ToString());

        var iterator = context.AITasks.GetItemQueryIterator<AIAgentTask>(query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(stepExecutionId.ToString()) });

        var results = new List<AIAgentTask>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }
        return results.AsReadOnly();
    }

    public async Task AddAsync(AIAgentTask task, CancellationToken cancellationToken = default)
    {
        await context.AITasks.CreateItemAsync(
            task,
            new PartitionKey(task.StepExecutionId.ToString()),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(AIAgentTask task, CancellationToken cancellationToken = default)
    {
        await context.AITasks.UpsertItemAsync(
            task,
            new PartitionKey(task.StepExecutionId.ToString()),
            cancellationToken: cancellationToken);
    }
}
