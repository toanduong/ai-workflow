using Microsoft.Azure.Cosmos;
using WorkflowAI.Domain.Approvals;

namespace WorkflowAI.Infrastructure.Persistence.Cosmos.Repositories;

public sealed class CosmosApprovalRepository(CosmosDbContext context) : IApprovalRepository
{
    public async Task<ApprovalRequest?> GetByIdAsync(ApprovalRequestId id, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.Value.ToString());

        var iterator = context.Approvals.GetItemQueryIterator<ApprovalRequest>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var result = response.FirstOrDefault();
            if (result is not null) return result;
        }
        return null;
    }

    public async Task<ApprovalRequest?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.approvalToken = @token")
            .WithParameter("@token", token);

        var iterator = context.Approvals.GetItemQueryIterator<ApprovalRequest>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var result = response.FirstOrDefault();
            if (result is not null) return result;
        }
        return null;
    }

    public async Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.status.name = 'Pending'");
        var iterator = context.Approvals.GetItemQueryIterator<ApprovalRequest>(query);
        var results = new List<ApprovalRequest>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<ApprovalRequest>> GetExpiredPendingAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.status.name = 'Pending' AND c.expiresAt <= @now")
            .WithParameter("@now", DateTime.UtcNow);

        var iterator = context.Approvals.GetItemQueryIterator<ApprovalRequest>(query);
        var results = new List<ApprovalRequest>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }
        return results.AsReadOnly();
    }

    public async Task AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        await context.Approvals.CreateItemAsync(
            request,
            new PartitionKey(request.StepExecutionId.ToString()),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        await context.Approvals.UpsertItemAsync(
            request,
            new PartitionKey(request.StepExecutionId.ToString()),
            cancellationToken: cancellationToken);
    }
}
