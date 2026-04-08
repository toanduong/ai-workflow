using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.TenantConnectors;

/// <summary>
/// Table 2: TenantConnectorApis
/// One row per API operation supported by a connector for a tenant.
/// e.g. Odoo → "res.partner/list" → GET /api/res.partner
///      Odoo → "sale.order/create" → POST /api/sale.order
///
/// Metadata JSON per row:
/// {
///   "method": "GET",
///   "url": "https://{odoo_instance}/api/res.partner",
///   "headers": { "Authorization": "$secret" },
///   "requestMapping":  { ... },
///   "responseMapping": { "records": "$.result" }
/// }
/// Claude populates these rows during the ValidateTenantConnector step.
/// </summary>
public sealed class TenantConnectorApi : Entity<TenantConnectorApiId>
{
    public TenantConnectorId TenantConnectorId { get; private set; }
    public TenantId TenantId { get; private set; }
    public string ConnectorType { get; private set; } = string.Empty;  // FK ref: same as TenantConnector.ConnectorType
    public string ApiName { get; private set; } = string.Empty;        // e.g. "res.partner/list", "sale.order/create"
    public string HttpMethod { get; private set; } = string.Empty;     // GET | POST | PUT | DELETE | PATCH
    public string UrlTemplate { get; private set; } = string.Empty;    // e.g. "https://{odoo_instance}/api/res.partner"
    public string Metadata { get; private set; } = string.Empty;       // Full JSON: headers, requestMapping, responseMapping
    public int Version { get; private set; } = 1;                      // Incremented when Claude re-discovers this API with schema changes

    private TenantConnectorApi() { }

    public static TenantConnectorApi Create(
        TenantConnectorId tenantConnectorId,
        TenantId tenantId,
        string connectorType,
        string apiName,
        string httpMethod,
        string urlTemplate,
        string metadata)
    {
        return new TenantConnectorApi
        {
            Id = TenantConnectorApiId.New(),
            TenantConnectorId = tenantConnectorId,
            TenantId = tenantId,
            ConnectorType = connectorType,
            ApiName = apiName,
            HttpMethod = httpMethod.ToUpperInvariant(),
            UrlTemplate = urlTemplate,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateMetadata(string metadata)
    {
        Metadata = metadata;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementVersion()
    {
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }
}
