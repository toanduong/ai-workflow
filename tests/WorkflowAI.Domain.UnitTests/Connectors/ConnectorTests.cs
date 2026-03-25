using FluentAssertions;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Domain.UnitTests.Connectors;

public class ConnectorTests
{
    [Fact]
    public void Create_ShouldSetCorrectDefaults()
    {
        var connector = Connector.Create(
            "Test Slack", ConnectorType.Slack, AuthModel.APIKey,
            UserId.New());

        connector.Id.Value.Should().NotBeEmpty();
        connector.Name.Should().Be("Test Slack");
        connector.ConnectorType.Should().Be(ConnectorType.Slack);
        connector.AuthModel.Should().Be(AuthModel.APIKey);
        connector.Status.Should().Be(ConnectorStatus.Created);
        connector.AzureApiConnectionId.Should().BeNull();
    }

    [Fact]
    public void Activate_ShouldChangeStatusToActive()
    {
        var connector = Connector.Create("Test", ConnectorType.ACS, AuthModel.ConnectionString, UserId.New());
        connector.Activate(DateTime.UtcNow.AddDays(30));

        connector.Status.Should().Be(ConnectorStatus.Active);
        connector.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_ShouldChangeStatusToFailed()
    {
        var connector = Connector.Create("Test", ConnectorType.Office365, AuthModel.OAuth2, UserId.New());
        connector.MarkFailed();

        connector.Status.Should().Be(ConnectorStatus.Failed);
    }

    [Fact]
    public void MarkExpired_ShouldChangeStatusToExpired()
    {
        var connector = Connector.Create("Test", ConnectorType.Teams, AuthModel.OAuth2, UserId.New());
        connector.MarkExpired();

        connector.Status.Should().Be(ConnectorStatus.Expired);
    }

    [Fact]
    public void SetApiConnectionId_ShouldStoreResourceId()
    {
        var connector = Connector.Create("Test", ConnectorType.Office365, AuthModel.OAuth2, UserId.New());
        connector.SetApiConnectionId("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Web/connections/o365");

        connector.AzureApiConnectionId.Should().Contain("Microsoft.Web/connections");
    }

    [Fact]
    public void SetCredential_ShouldStoreCredentialId()
    {
        var connector = Connector.Create("Test", ConnectorType.Slack, AuthModel.APIKey, UserId.New());
        var credentialId = Guid.NewGuid();
        connector.SetCredential(credentialId);

        connector.CredentialId.Should().Be(credentialId);
    }
}
