using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.Common.Builders;

/// <summary>
/// Fluent test builder for TenantConnector.
///
/// The metadata JSON stored here simulates what Claude AI would generate during Provision.
/// The application code has no concept of "connector types" — Claude decides everything.
/// Use WithMetadata() to set whatever Claude would return for the connector under test.
/// </summary>
public sealed class TenantConnectorBuilder
{
    private TenantId _tenantId = TenantId.New();
    private string _connectorName = "TestConnector";

    // Default: Claude returned a fixed baseUrl (common for SaaS — Apollo, HubSpot, Stripe, etc.)
    // requiredFields: ["api_key"] → admin fills one field
    private string _metadata = """
        {
          "authType": "APIKey",
          "baseUrl": "https://api.example.com",
          "testEndpoint": { "method": "GET", "path": "/health" },
          "requiredFields": ["api_key"],
          "configSchema": {
            "api_key": { "type": "string", "description": "API key", "required": true }
          }
        }
        """;

    private string _info = """
        {
          "description": "A test connector",
          "docsUrl": "https://docs.example.com",
          "capabilities": ["read", "write"],
          "webhookSupport": false
        }
        """;

    private bool _activated;
    private bool _failed;
    private string? _failureReason;

    public TenantConnectorBuilder WithTenantId(TenantId tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public TenantConnectorBuilder WithConnectorType(string name)
    {
        _connectorName = name;
        return this;
    }

    /// <summary>
    /// Sets the Claude-generated metadata and info blobs directly.
    /// Use this to simulate exactly what Claude would return for the connector under test.
    /// </summary>
    public TenantConnectorBuilder WithMetadata(string metadata, string info)
    {
        _metadata = metadata;
        _info = info;
        return this;
    }

    public TenantConnectorBuilder Activated()
    {
        _activated = true;
        _failed = false;
        return this;
    }

    public TenantConnectorBuilder Failed(string reason = "Connection test failed.")
    {
        _failed = true;
        _activated = false;
        _failureReason = reason;
        return this;
    }

    public TenantConnector Build()
    {
        var connector = TenantConnector.Create(_tenantId, _connectorName);
        connector.SetMetadata(_metadata, _info);

        if (_activated) connector.Activate();
        else if (_failed) connector.MarkFailed(_failureReason!);

        return connector;
    }
}
