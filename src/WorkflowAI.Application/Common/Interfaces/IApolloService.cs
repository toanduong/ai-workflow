namespace WorkflowAI.Application.Common.Interfaces;

public interface IApolloService
{
    Task<ApolloContactResult?> GetContactAsync(string contactId, CancellationToken cancellationToken = default);
    Task<bool> UpdateContactAsync(string contactId, Dictionary<string, object> fields, CancellationToken cancellationToken = default);
    Task<bool> AddToSequenceAsync(string contactId, string sequenceId, CancellationToken cancellationToken = default);
}

public sealed record ApolloContactResult(
    string Id,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Title,
    string? Organization);
