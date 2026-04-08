using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.TenantConnectors;

/// <summary>
/// Table: TenantConnectorApiHealthChecks
/// Append-only audit log — one row per API test execution.
/// Records the outcome of calling a specific TenantConnectorApi endpoint.
/// </summary>
public sealed class TenantConnectorApiHealthCheck : Entity<TenantConnectorApiHealthCheckId>
{
    public TenantConnectorApiId TenantConnectorApiId { get; private set; }
    public TenantConnectorId TenantConnectorId { get; private set; }   // Denorm for connector-level queries
    public TenantId TenantId { get; private set; }                     // Tenant isolation
    public string ApiName { get; private set; } = string.Empty;        // Denorm from TenantConnectorApi
    public string HttpMethod { get; private set; } = string.Empty;
    public string ResolvedUrl { get; private set; } = string.Empty;    // Placeholders substituted at check time
    public DateTime CheckedAt { get; private set; }
    public bool IsSuccess { get; private set; }
    public int? StatusCode { get; private set; }                       // Null if no HTTP response (timeout/network error)
    public long DurationMs { get; private set; }
    public string? FailureReason { get; private set; }                 // Null on success

    private TenantConnectorApiHealthCheck() { }

    public static TenantConnectorApiHealthCheck Create(
        TenantConnectorApiId tenantConnectorApiId,
        TenantConnectorId tenantConnectorId,
        TenantId tenantId,
        string apiName,
        string httpMethod,
        string resolvedUrl,
        bool isSuccess,
        int? statusCode,
        long durationMs,
        string? failureReason = null)
    {
        return new TenantConnectorApiHealthCheck
        {
            Id = TenantConnectorApiHealthCheckId.New(),
            TenantConnectorApiId = tenantConnectorApiId,
            TenantConnectorId = tenantConnectorId,
            TenantId = tenantId,
            ApiName = apiName,
            HttpMethod = httpMethod.ToUpperInvariant(),
            ResolvedUrl = resolvedUrl,
            CheckedAt = DateTime.UtcNow,
            IsSuccess = isSuccess,
            StatusCode = statusCode,
            DurationMs = durationMs,
            FailureReason = failureReason is { Length: > 1000 }
                ? failureReason[..1000]
                : failureReason,
            CreatedAt = DateTime.UtcNow
        };
    }
}
