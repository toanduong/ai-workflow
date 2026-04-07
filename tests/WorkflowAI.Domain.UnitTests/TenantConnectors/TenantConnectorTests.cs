using FluentAssertions;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Domain.UnitTests.TenantConnectors;

public class TenantConnectorTests
{
    private static TenantConnector CreateOdooConnector() =>
        TenantConnector.Create(TenantId.New(), "Odoo");

    [Fact]
    public void Create_ShouldSetCorrectDefaults()
    {
        var tenantId = TenantId.New();
        var connector = TenantConnector.Create(tenantId, "Odoo");

        connector.Id.Value.Should().NotBeEmpty();
        connector.TenantId.Should().Be(tenantId);
        connector.ConnectorName.Should().Be("Odoo");
        connector.Status.Should().Be(TenantConnectorStatus.Pending);
        connector.Metadata.Should().BeEmpty();
        connector.Info.Should().BeEmpty();
        connector.FailureReason.Should().BeNull();
    }

    [Fact]
    public void SetMetadata_ShouldStoreMetadataAndInfo()
    {
        var connector = CreateOdooConnector();
        var metadata = """{"authType":"APIKey","testEndpoint":{"method":"GET","path":"/api/health"}}""";
        var info = """{"description":"Odoo ERP","docsUrl":"https://www.odoo.com/documentation"}""";

        connector.SetMetadata(metadata, info);

        connector.Metadata.Should().Be(metadata);
        connector.Info.Should().Be(info);
        connector.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Activate_ShouldChangeStatusToActive()
    {
        var connector = CreateOdooConnector();

        connector.Activate();

        connector.Status.Should().Be(TenantConnectorStatus.Active);
        connector.FailureReason.Should().BeNull();
        connector.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_ShouldChangeStatusToFailed_AndStoreReason()
    {
        var connector = CreateOdooConnector();

        connector.MarkFailed("HTTP 401: Unauthorized");

        connector.Status.Should().Be(TenantConnectorStatus.Failed);
        connector.FailureReason.Should().Be("HTTP 401: Unauthorized");
        connector.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Suspend_ShouldChangeStatusToSuspended()
    {
        var connector = CreateOdooConnector();
        connector.Activate();

        connector.Suspend();

        connector.Status.Should().Be(TenantConnectorStatus.Suspended);
    }

    [Fact]
    public void Activate_AfterFailed_ShouldClearFailureReason()
    {
        var connector = CreateOdooConnector();
        connector.MarkFailed("some error");
        connector.Status.Should().Be(TenantConnectorStatus.Failed);

        connector.Activate();

        connector.Status.Should().Be(TenantConnectorStatus.Active);
        connector.FailureReason.Should().BeNull();
    }
}
