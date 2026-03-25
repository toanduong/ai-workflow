using Microsoft.Azure.Cosmos;

namespace WorkflowAI.Infrastructure.Persistence.Cosmos;

public sealed class CosmosDbContext
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;

    public CosmosDbContext(CosmosClient client, string databaseName)
    {
        _client = client;
        _databaseName = databaseName;
    }

    public Container Workflows => _client.GetContainer(_databaseName, CosmosContainerNames.Workflows);
    public Container Executions => _client.GetContainer(_databaseName, CosmosContainerNames.Executions);
    public Container Approvals => _client.GetContainer(_databaseName, CosmosContainerNames.Approvals);
    public Container Notifications => _client.GetContainer(_databaseName, CosmosContainerNames.Notifications);
    public Container AITasks => _client.GetContainer(_databaseName, CosmosContainerNames.AITasks);

    public async Task InitializeAsync()
    {
        var database = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);

        await database.Database.CreateContainerIfNotExistsAsync(CosmosContainerNames.Workflows, "/id");
        await database.Database.CreateContainerIfNotExistsAsync(CosmosContainerNames.Executions, "/workflowId");
        await database.Database.CreateContainerIfNotExistsAsync(CosmosContainerNames.Approvals, "/stepExecutionId");
        await database.Database.CreateContainerIfNotExistsAsync(CosmosContainerNames.Notifications, "/approvalRequestId");
        await database.Database.CreateContainerIfNotExistsAsync(CosmosContainerNames.AITasks, "/stepExecutionId");
    }
}
