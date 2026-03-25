using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Domain.Connectors;

public sealed class Connector : Entity<ConnectorId>
{
    public string Name { get; private set; } = string.Empty;
    public ConnectorType ConnectorType { get; private set; } = ConnectorType.Custom;
    public AuthModel AuthModel { get; private set; } = AuthModel.APIKey;
    public ConnectorStatus Status { get; private set; } = ConnectorStatus.Created;
    public Guid? CredentialId { get; private set; }
    public string? AzureApiConnectionId { get; private set; }
    public string? ManagedApiId { get; private set; }
    public string? Configuration { get; private set; }
    public UserId CreatedByUserId { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    private Connector() { }

    public static Connector Create(
        string name,
        ConnectorType connectorType,
        AuthModel authModel,
        UserId createdByUserId,
        string? configuration = null,
        string? managedApiId = null)
    {
        return new Connector
        {
            Id = ConnectorId.New(),
            Name = name,
            ConnectorType = connectorType,
            AuthModel = authModel,
            CreatedByUserId = createdByUserId,
            Configuration = configuration,
            ManagedApiId = managedApiId,
            Status = ConnectorStatus.Created
        };
    }

    public void SetCredential(Guid credentialId) { CredentialId = credentialId; UpdatedAt = DateTime.UtcNow; }
    public void Activate(DateTime? expiresAt = null) { Status = ConnectorStatus.Active; ExpiresAt = expiresAt; UpdatedAt = DateTime.UtcNow; }
    public void MarkFailed() { Status = ConnectorStatus.Failed; UpdatedAt = DateTime.UtcNow; }
    public void MarkExpired() { Status = ConnectorStatus.Expired; UpdatedAt = DateTime.UtcNow; }
    public void StartValidation() { Status = ConnectorStatus.Validating; UpdatedAt = DateTime.UtcNow; }
    public void SetApiConnectionId(string azureApiConnectionId) { AzureApiConnectionId = azureApiConnectionId; UpdatedAt = DateTime.UtcNow; }
    public void UpdateConfiguration(string? configuration) { Configuration = configuration; UpdatedAt = DateTime.UtcNow; }
}
