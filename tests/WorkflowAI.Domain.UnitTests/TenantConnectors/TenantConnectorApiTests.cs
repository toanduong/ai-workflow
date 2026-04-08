using FluentAssertions;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Domain.UnitTests.TenantConnectors;

public class TenantConnectorApiTests
{
    private static readonly TenantConnectorId ConnectorId = TenantConnectorId.New();
    private static readonly TenantId TenantId = Domain.TenantConnectors.TenantId.New();

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var metadata = """{"headers":{"Authorization":"Bearer $secret"},"requestMapping":{}}""";

        var api = TenantConnectorApi.Create(
            ConnectorId,
            TenantId,
            "Odoo",
            "res.partner/list",
            "get",
            "https://{odoo_instance}/api/res.partner",
            metadata);

        api.Id.Value.Should().NotBeEmpty();
        api.TenantConnectorId.Should().Be(ConnectorId);
        api.TenantId.Should().Be(TenantId);
        api.ConnectorType.Should().Be("Odoo");
        api.ApiName.Should().Be("res.partner/list");
        api.HttpMethod.Should().Be("GET");  // normalised to upper
        api.UrlTemplate.Should().Be("https://{odoo_instance}/api/res.partner");
        api.Metadata.Should().Be(metadata);
        api.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldNormaliseHttpMethodToUpperCase()
    {
        var api = TenantConnectorApi.Create(
            ConnectorId, TenantId, "Odoo", "sale.order/create", "post",
            "https://{odoo_instance}/api/sale.order", "{}");

        api.HttpMethod.Should().Be("POST");
    }

    [Fact]
    public void UpdateMetadata_ShouldReplaceMetadataAndSetUpdatedAt()
    {
        var api = TenantConnectorApi.Create(
            ConnectorId, TenantId, "Odoo", "res.partner/list", "GET",
            "https://{odoo_instance}/api/res.partner", "{}");

        var newMeta = """{"headers":{"X-Custom":"value"}}""";
        api.UpdateMetadata(newMeta);

        api.Metadata.Should().Be(newMeta);
        api.UpdatedAt.Should().NotBeNull();
    }
}
