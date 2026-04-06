# Dynamic Multi-Tenant Connector Management

## What Problem This Solves

The original system had connectors hardcoded — every new 3rd party integration (Apollo, Salesforce, Slack, etc.) required code changes to add a new `ConnectorType` enum value and write custom integration logic.

This new system makes connectors **fully dynamic**:
- Admin types a connector name (e.g. `"Apollo"`) → Claude AI generates all integration metadata automatically
- No code changes needed to support a new 3rd party
- Each company (tenant) manages their own connectors independently
- Generated API routes and workflow templates are saved to the database and immediately usable in the workflow builder

---

## Database Table

One new table: **`TenantConnectors`**

| Column | Type | Description |
|--------|------|-------------|
| Id | uuid | Primary key |
| TenantId | uuid | Company identifier |
| ConnectorName | varchar(200) | e.g. `"Apollo"`, `"Salesforce"`, `"Stripe"` |
| Metadata | text (JSON) | Claude-generated: authType, requiredFields, endpoints, testEndpoint, configSchema |
| Info | text (JSON) | Claude-generated: description, docsUrl, capabilities, rateLimits, webhookSupport |
| Status | varchar(50) | `Pending` → `Active` / `Failed` / `Suspended` |
| FailureReason | varchar(1000) | Set when Status = Failed |
| CreatedAt | timestamp | |
| UpdatedAt | timestamp | |

Unique constraint on `(TenantId, ConnectorName)` — one connector per tenant per 3rd party.

---

## Full Flow

```
1. Admin types "Apollo"
        ↓
2. POST /tenants/{tenantId}/connectors
   { "connectorName": "Apollo" }
        ↓
3. Claude AI generates Metadata + Info JSON
   Metadata: { authType, requiredFields, endpoints, testEndpoint, configSchema }
   Info:     { description, docsUrl, capabilities, rateLimits, webhookSupport }
        ↓
4. TenantConnector saved to DB (Status = Pending)
        ↓
5. Frontend reads requiredFields → renders form for user to fill in
   e.g. Apollo → shows "API Key" input field
        ↓
6. POST /tenants/{tenantId}/connectors/{id}/validate
   { "credentialFields": { "api_key": "2KnD..." } }
        ↓
7. Handler reads authType from Metadata, applies credentials to HTTP request:
   - APIKey  → X-Api-Key header
   - Bearer  → Authorization: Bearer
   - Basic   → Authorization: Basic (base64 username:password)
   - OAuth2  → Authorization: Bearer (access_token)
   Calls testEndpoint from Metadata
        ↓
8. If 2xx → Status = Active
   If error → Status = Failed (reason saved)
        ↓
9. POST /tenants/{tenantId}/connectors/{id}/generate
        ↓
10. Claude AI reads Metadata + Info, calls tools:
    - create_api_route (×3+) → each saved as WorkflowTemplate (Category = "Apollo/ApiRoute")
    - create_workflow_template (×2+) → each saved as WorkflowTemplate (Category = "Apollo/Workflow")
        ↓
11. Templates appear in workflow builder, ready to use
```

---

## API Endpoints

| Method | Route | What it does |
|--------|-------|-------------|
| POST | `/tenants/{tenantId}/connectors` | Provision: Claude generates Metadata + Info |
| GET | `/tenants/{tenantId}/connectors` | List all connectors for a tenant |
| GET | `/tenants/{tenantId}/connectors/{id}` | Get a single connector |
| POST | `/tenants/{tenantId}/connectors/{id}/validate` | Test connection with user-supplied credentials |
| POST | `/tenants/{tenantId}/connectors/{id}/generate` | Generate API routes + workflow templates via Claude |
| DELETE | `/tenants/{tenantId}/connectors/{id}` | Remove a connector |

---

## What Claude Generates

### Metadata (technical)
```json
{
  "authType": "APIKey",
  "requiredFields": ["api_key"],
  "testEndpoint": { "method": "GET", "path": "https://api.apollo.io/v1/auth/health" },
  "endpoints": {
    "getContact": { "method": "GET", "path": "/people/match" },
    "searchPeople": { "method": "POST", "path": "/mixed_people/search" }
  },
  "configSchema": {
    "type": "object",
    "properties": {
      "api_key": { "type": "string", "description": "Apollo API Key" }
    }
  }
}
```

### Info (human-readable)
```json
{
  "description": "Apollo.io is a sales intelligence and engagement platform.",
  "docsUrl": "https://apolloio.github.io/apollo-api-docs/",
  "capabilities": ["contact_search", "email_sequences", "crm_sync", "lead_enrichment"],
  "rateLimits": "50 requests/minute on free tier",
  "webhookSupport": true
}
```

---

## Auth Types Supported

| authType | How credentials are applied | Required fields |
|----------|-----------------------------|-----------------|
| `APIKey` | `X-Api-Key` header | `api_key` |
| `Bearer` | `Authorization: Bearer {token}` | `token` or `access_token` |
| `OAuth2` | `Authorization: Bearer {access_token}` | `access_token` |
| `Basic` | `Authorization: Basic {base64(user:pass)}` | `username`, `password` |
| `ConnectionString` | Not applied as header (service-level) | `connection_string` |

---

## Generated Templates in WorkflowTemplates Table

After calling `/generate`, Claude creates real `WorkflowTemplate` records:

| Category | Example Name | DefaultSteps |
|----------|-------------|--------------|
| `Apollo/ApiRoute` | `Apollo: GET /people/match` | HTTP step with method + config JSON |
| `Apollo/ApiRoute` | `Apollo: POST /sequences/enroll` | HTTP step with method + config JSON |
| `Apollo/Workflow` | `Apollo: Apollo Lead Enrich` | Claude-generated step array |
| `Apollo/Workflow` | `Apollo: Apollo Sequence Trigger` | Claude-generated step array |

These immediately appear in `GET /templates` and can be used in the drag-and-drop workflow builder.

---

## Code Locations

| Layer | Path |
|-------|------|
| Domain entity | `src/WorkflowAI.Domain/TenantConnectors/TenantConnector.cs` |
| Domain value objects | `src/WorkflowAI.Domain/TenantConnectors/TenantId.cs`, `TenantConnectorId.cs`, `TenantConnectorStatus.cs` |
| Repository interface | `src/WorkflowAI.Domain/TenantConnectors/ITenantConnectorRepository.cs` |
| Domain events | `src/WorkflowAI.Domain/TenantConnectors/Events/` |
| Provision command | `src/WorkflowAI.Application/TenantConnectors/Commands/ProvisionTenantConnector/` |
| Validate command | `src/WorkflowAI.Application/TenantConnectors/Commands/ValidateTenantConnector/` |
| Generate command | `src/WorkflowAI.Application/TenantConnectors/Commands/GenerateMcpWorkflows/` |
| List/Get queries | `src/WorkflowAI.Application/TenantConnectors/Queries/` |
| EF configuration | `src/WorkflowAI.Infrastructure/Persistence/EntityFramework/Configurations/TenantConnectorConfiguration.cs` |
| SQL repository | `src/WorkflowAI.Infrastructure/Persistence/EntityFramework/Repositories/SqlTenantConnectorRepository.cs` |
| HTTP endpoints | `src/WorkflowAI.Functions/HttpTriggers/TenantConnectors/` |
| Unit tests | `tests/WorkflowAI.Application.UnitTests/TenantConnectors/` |
| Apollo integration tests | `tests/WorkflowAI.Infrastructure.IntegrationTests/TenantConnectors/ApolloConnectionTests.cs` |

---

## Test Coverage

### Unit Tests (18 passing)

| Test class | Tests | What is covered |
|------------|-------|-----------------|
| `ProvisionTenantConnectorCommandHandlerTests` | 5 | Claude called, conflict check, invalid JSON, failed AI, multiple connectors |
| `ValidateTenantConnectorCommandHandlerTests` | 8 | APIKey auth, invalid key, Bearer header, Basic base64, not found, no testEndpoint, network failure |
| `GenerateMcpWorkflowsCommandHandlerTests` | 6 | Templates saved, correct categories, pending/failed/not-found blocked, Claude failure |

### Integration Tests (4 passing — live Apollo API)

| Test | What it proves |
|------|---------------|
| `ApolloApiKey_IsValid_WhenHealthCheckShowsLoggedIn` | Key `2KnD...` is valid, Apollo returns `is_logged_in: true` |
| `ApolloApiKey_IsInvalid_WhenHealthCheckShowsNotLoggedIn` | Wrong keys return `is_logged_in: false` |
| `Apollo_PeopleSearch_Returns403_OnFreeTierPlanRestriction` | People search needs paid plan |
| `ValidateTenantConnectorFlow_WithApolloMetadata_ActivatesSuccessfully` | Full end-to-end: metadata → testEndpoint → Apollo auth confirmed |

---

## What Is Not Yet Built

- **Secret storage** — credentials submitted to `/validate` are used once but not persisted. A production system would store them in Azure Key Vault via `IKeyVaultService` and reference them by secret name in `TenantConnector`
- **OAuth2 redirect flow** — the current system accepts a pre-obtained `access_token`. A full OAuth2 flow needs a consent URL + callback endpoint
- **Webhook receiver** — for connectors like Apollo that push events, a webhook endpoint needs to be registered and routed into the workflow engine
- **Tenant management** — `TenantId` is a plain Guid passed by the caller. A real system would have a `Tenants` table with authentication ensuring a user can only access their own tenant's connectors
