# Dynamic Multi-Tenant Connector Management — Master Document

> Generated: 2026-04-06  
> Combines: current-state analysis + implementation plan

---

## Executive Summary

| Feature | Status |
|---|---|
| Connector Management (CRUD) | **Fully Implemented** (single-tenant, hardcoded type) |
| Connector Metadata Storage | **Fully Implemented** |
| Connector Credential Management | **Fully Implemented** |
| Claude SDK Registered | **Yes (DI wired up)** |
| Claude SDK Used in Connector Flow | **NOT implemented** |
| Multi-Tenant Connector Table | **NOT implemented** |
| Admin → Create Connector → Claude → Store | **Partial — Claude step is missing** |
| MCP Tool Layer | **NOT implemented** |

**The problem:** The current `Connector` entity is hardcoded to a `ConnectorType` enum and tied to `UserId`. Every new 3rd-party integration requires a code change. The goal is a **dynamic, tenant-aware framework** where Claude AI generates all integration metadata on demand.

---

## Part 1 — Current State (What Is Built)

### 1.1 Connector Domain Model

**File:** `src/WorkflowAI.Domain/Connectors/Connector.cs`

Stores:
- `Name`, `ConnectorType` (enum — hardcoded), `AuthModel`, `Status`
- `CredentialId` → Azure Key Vault reference
- `AzureApiConnectionId`, `ManagedApiId`
- `Configuration` (JSON blob) — endpoint, method, headers, mapping data
- `CreatedByUserId`, `ExpiresAt`

State machine: `Created → Validating → Active / Failed / Expired`

### 1.2 Application Layer (CQRS)

**Directory:** `src/WorkflowAI.Application/Connectors/Commands/`

| Command / Query | Handler |
|---|---|
| Create Connector | `CreateConnector/CreateConnectorCommandHandler.cs` |
| Delete Connector | `DeleteConnector/` |
| Validate Connector | `ValidateConnector/` |
| List Connectors | `Queries/` |
| Get Connector | `Queries/` |

### 1.3 HTTP API Endpoints

**Directory:** `src/WorkflowAI.Functions/HttpTriggers/Connectors/`

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/connectors` | Create connector |
| GET | `/api/connectors` | List all connectors |
| GET | `/api/connectors/{id}` | Get connector |
| DELETE | `/api/connectors/{id}` | Delete connector |
| POST | `/api/connectors/{id}/validate` | Validate connector |
| POST | `/api/connectors/oauth/callback` | OAuth callback |

### 1.4 Auth Models Supported

- OAuth2 (consent URL generation)
- API Key
- Connection String
- Managed Identity
- Service Principal

### 1.5 Config-Driven JSON (matches whiteboard)

`Connector.Configuration` stores per-connector config:
```json
{
  "endpoint": "https://api.example.com",
  "method": "POST",
  "headers": { "Authorization": "$secret" },
  "page_id": "123",
  "page_access_token": "$secret"
}
```

`ConnectorCredentialResolver.cs` resolves `$secret` placeholders from Azure Key Vault at execution time — this is the **data transformation / mapping layer** from the whiteboard.

### 1.6 Claude SDK — Registered but Unused

| Layer | File | Status |
|---|---|---|
| Interface | `src/WorkflowAI.Application/Common/Interfaces/IAnthropicService.cs` | Defined |
| Implementation | `src/WorkflowAI.Infrastructure/AI/AnthropicService.cs` | Complete |
| Options | `src/WorkflowAI.Infrastructure/AI/AnthropicOptions.cs` | Complete |
| DI Registration | `src/WorkflowAI.Infrastructure/DependencyInjection.cs` (lines 73–80) | Registered |
| Usage in connectors | — | **ZERO usages** |

```csharp
// IAnthropicService.cs
Task<AICompletionResult> CompleteAsync(string prompt, string? model, CancellationToken ct);
Task<AICompletionResult> CompleteWithToolsAsync(string prompt, IEnumerable<AIToolDefinition> tools, ...);
```

```csharp
// DependencyInjection.cs — already wired
services.Configure<AnthropicOptions>(configuration.GetSection("Anthropic"));
services.AddSingleton<IChatClient>(sp => {
    var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
    return new AnthropicClient(new APIAuthentication(opts.ApiKey)).Messages;
});
services.AddScoped<IAnthropicService, AnthropicService>();
```

### 1.7 Current Connector Flow

```
POST /api/connectors
  └─ CreateConnectorCommandHandler
        ├─ Connector.Create()
        ├─ (OAuth2) → ConnectorService.GenerateOAuthConsentUrl()
        ├─ (APIKey)  → Key Vault store → ConnectorCredential saved
        └─ PostgreSQL save → return ConnectorId
```

**Claude is never called.** Metadata is manually provided by the admin.

---

## Part 2 — Target Architecture (What Needs to Be Built)

### 2.1 Vision

```
Admin API Request
  → ProvisionTenantConnectorCommand
    → Claude AI generates Metadata + Info
      → TenantConnector saved to DB
        → ValidateTenantConnectorCommand
          → Tests connection using Metadata
            → MCP generates API routes + Workflow definition
              → Connector ready for use
```

### 2.2 Two-Table Data Model

The validation flow spans **two tables** — one for the connector definition, one for the API-level configuration per connector.

---

#### Table 1: `TenantConnectors` — Connector Definition

> Claude generates this on first provision. Describes *what* the connector is.

| Column | Type | Description |
|---|---|---|
| TenantId | Guid | Company/tenant identifier |
| Connector | string | 3rd party name (e.g. "Apollo", "Odoo") |
| Metadata | JSON | Claude-generated: auth type, required fields, config schema, test endpoint |
| Info | JSON | Claude-generated: description, docs URL, capabilities, rate limits |

---

#### Table 2: `TenantConnectorApis` — API-Level Configuration

> Populated after validation. Describes *how* to call each API endpoint of the connector.

| Column | Type | Description |
|---|---|---|
| Tenant | Guid | FK → `TenantConnectors.TenantId` |
| Connector | string | FK → `TenantConnectors.Connector` |
| API | string | Specific API endpoint or operation name (e.g. `contacts/search`) |
| Metadata | JSON | Runtime config: HTTP method, URL, headers, request/response mapping |

---

**Relationship:**

```
TenantConnectors (1)
  └─── TenantConnectorApis (many)
         One connector → many API operations
         e.g. Apollo → contacts/search, sequences/enroll, people/match
```

This replaces the hardcoded `ConnectorType` enum with a fully dynamic, two-level, Claude-populated structure.

### 2.3 Target Flow

```
Admin configures connector settings
  └─ POST /tenants/{tenantId}/connectors   { connectorName: "Apollo" }
        └─ ProvisionTenantConnectorCommandHandler
              ├─ Conflict check (TenantId + ConnectorName unique)
              ├─ IAnthropicService.CompleteAsync(prompt)        ← THE MISSING STEP
              │     → Claude generates Metadata + Info JSON
              ├─ TenantConnector.Create() → SetMetadata()
              ├─ Save to Table 1: TenantConnectors
              └─ return TenantConnectorId + Metadata + Info

  └─ POST /tenants/{tenantId}/connectors/{id}/validate   { apiKey: "..." }
        └─ ValidateTenantConnectorCommandHandler
              ├─ Load Table 1 → extract testEndpoint from Metadata
              ├─ HTTP call to testEndpoint with provided credential
              ├─ On success → Activate() → update Table 1 status
              └─ IAnthropicService.CompleteWithToolsAsync()     ← POPULATE TABLE 2
                    → Claude enumerates all API operations
                    → Each operation → insert row into Table 2: TenantConnectorApis
                       (Tenant, Connector, API name, per-API Metadata)
```

### 2.4 Claude Prompt Template

```
You are a connector metadata generator. For the 3rd party service "{connectorName}", generate two JSON objects:

1. METADATA: Technical integration details including:
   - authType: (APIKey|OAuth2|Basic|Bearer)
   - requiredFields: array of field names needed for connection
   - endpoints: key API endpoints with HTTP method and path
   - configSchema: JSON Schema for configuration fields
   - testEndpoint: endpoint to use for connection validation

2. INFO: Human-readable information including:
   - description: what the service does
   - docsUrl: official API documentation URL
   - capabilities: array of what can be automated
   - rateLimits: known rate limiting info
   - webhookSupport: boolean

Return ONLY valid JSON in this format:
{"metadata": {...}, "info": {...}}
```

---

## Part 3 — Implementation Plan

### Phase 1: Domain Layer

**Files to create under `src/WorkflowAI.Domain/TenantConnectors/`:**

- `TenantId.cs` — value object `readonly record struct TenantId(Guid Value)`
- `TenantConnectorId.cs` — value object (Table 1 PK)
- `TenantConnectorApiId.cs` — value object (Table 2 PK)
- `TenantConnectorStatus.cs` — enumeration: `Pending`, `Active`, `Failed`, `Suspended`
- `TenantConnector.cs` — aggregate root **(Table 1)**
  - Properties: `TenantId`, `ConnectorName`, `Metadata` (JSON), `Info` (JSON), `Status`, `CreatedAt`, `UpdatedAt`
  - Factory: `TenantConnector.Create(TenantId, string connectorName)`
  - Methods: `SetMetadata(string metadata, string info)`, `Activate()`, `MarkFailed(string reason)`, `Suspend()`
  - Events: `TenantConnectorProvisionedEvent`, `TenantConnectorActivatedEvent`
- `ITenantConnectorRepository.cs`

```csharp
Task AddAsync(TenantConnector connector, CancellationToken ct = default);
Task<TenantConnector?> GetByIdAsync(TenantConnectorId id, CancellationToken ct = default);
Task<IReadOnlyList<TenantConnector>> GetByTenantAsync(TenantId tenantId, CancellationToken ct = default);
Task<TenantConnector?> GetByTenantAndConnectorAsync(TenantId tenantId, string connectorName, CancellationToken ct = default);
Task UpdateAsync(TenantConnector connector, CancellationToken ct = default);
Task DeleteAsync(TenantConnectorId id, CancellationToken ct = default);
```

---

### Phase 2: Application Layer

#### Command: Provision Tenant Connector

**`src/WorkflowAI.Application/TenantConnectors/Commands/ProvisionTenantConnector/`**

```csharp
public sealed record ProvisionTenantConnectorCommand(
    Guid TenantId,
    string ConnectorName   // e.g. "Apollo", "Salesforce", "Odoo"
) : IRequest<Result<ProvisionTenantConnectorResult>>;

public sealed record ProvisionTenantConnectorResult(
    Guid TenantConnectorId,
    string Metadata,
    string Info
);
```

Handler steps:
1. Conflict check — return `Error.Conflict` if `TenantId + ConnectorName` already exists
2. Call `IAnthropicService.CompleteAsync(prompt)` → parse Metadata + Info JSON
3. `TenantConnector.Create()` → `SetMetadata()`
4. Save to repository → return result

#### Command: Validate Tenant Connector

```csharp
public sealed record ValidateTenantConnectorCommand(
    Guid TenantConnectorId,
    string ApiKey
) : IRequest<Result<bool>>;
```

Handler: load connector → extract `testEndpoint` from Metadata → HTTP call → `Activate()` or `MarkFailed()`.

#### Command: Generate MCP Workflows

```csharp
public sealed record GenerateMcpWorkflowsCommand(
    Guid TenantConnectorId
) : IRequest<Result<GenerateMcpWorkflowsResult>>;

public sealed record GenerateMcpWorkflowsResult(
    string ApiRoutes,
    string WorkflowDefs
);
```

Handler: load Active connector → call `IAnthropicService.CompleteWithToolsAsync()` with tools:
- `create_api_route` — defines a new API endpoint
- `create_workflow_template` — defines a workflow

#### Queries

- `ListTenantConnectorsQuery(Guid TenantId)` → `IReadOnlyList<TenantConnectorDto>`
- `GetTenantConnectorQuery(Guid TenantConnectorId)` → `TenantConnectorDto`

```csharp
public sealed record TenantConnectorDto(
    Guid Id, Guid TenantId, string ConnectorName,
    string Metadata, string Info, string Status,
    DateTime CreatedAt, DateTime? UpdatedAt
);
```

---

### Phase 3: Infrastructure Layer

**`TenantConnectorConfiguration.cs`** (EF Core):

```csharp
builder.ToTable("TenantConnectors");
builder.Property(e => e.ConnectorName).HasMaxLength(200).IsRequired();
builder.Property(e => e.Metadata).HasColumnType("text").IsRequired();
builder.Property(e => e.Info).HasColumnType("text").IsRequired();
builder.HasIndex(new[] { "TenantId", "ConnectorName" }).IsUnique();
```

**`SqlTenantConnectorRepository.cs`** — follows `SqlApolloEventRepository` pattern.

**DbContext:** add `public DbSet<TenantConnector> TenantConnectors => Set<TenantConnector>();`

**DI:** add `services.AddScoped<ITenantConnectorRepository, SqlTenantConnectorRepository>();`

---

### Phase 4: HTTP API (Azure Functions)

**`src/WorkflowAI.Functions/TenantConnectors/`**

| Method | Route | Command/Query |
|---|---|---|
| POST | `/tenants/{tenantId}/connectors` | `ProvisionTenantConnectorCommand` |
| GET | `/tenants/{tenantId}/connectors` | `ListTenantConnectorsQuery` |
| GET | `/tenants/{tenantId}/connectors/{id}` | `GetTenantConnectorQuery` |
| POST | `/tenants/{tenantId}/connectors/{id}/validate` | `ValidateTenantConnectorCommand` |
| POST | `/tenants/{tenantId}/connectors/{id}/generate` | `GenerateMcpWorkflowsCommand` |
| DELETE | `/tenants/{tenantId}/connectors/{id}` | Delete command |

---

### Phase 5: Unit Tests

**`tests/WorkflowAI.Application.UnitTests/TenantConnectors/`**

| Test Class | Key Scenarios |
|---|---|
| `ProvisionTenantConnectorCommandHandlerTests` | New connector succeeds; duplicate returns Conflict; Claude fails returns Failure; empty TenantId returns Validation |
| `ValidateTenantConnectorCommandHandlerTests` | Valid API key activates; invalid returns Failed; not found returns NotFound |
| `GenerateMcpWorkflowsCommandHandlerTests` | Active connector returns routes; inactive returns Validation error |

**Test builder** (`TenantConnectorBuilder` — follow `ConnectorBuilder` pattern):
```csharp
new TenantConnectorBuilder()
    .WithTenantId(TenantId.New())
    .WithConnectorName("Apollo")
    .WithMetadata("{...}", "{...}")
    .Activated()
    .Build()
```

---

### Phase 6: Apollo End-to-End Example

1. `POST /tenants/{tenantId}/connectors` → `{ "connectorName": "Apollo" }`
2. Claude auto-generates:
   ```json
   {
     "metadata": {
       "authType": "APIKey",
       "requiredFields": ["api_key"],
       "testEndpoint": { "method": "GET", "path": "https://api.apollo.io/v1/auth/health" },
       "configSchema": { ... }
     },
     "info": {
       "description": "Apollo.io is a sales intelligence platform",
       "docsUrl": "https://apolloio.github.io/apollo-api-docs/",
       "capabilities": ["contact_search", "email_sequences", "crm_sync"],
       "webhookSupport": true
     }
   }
   ```
3. `POST /tenants/{tenantId}/connectors/{id}/validate` with API key → status: `Active`
4. `POST /tenants/{tenantId}/connectors/{id}/generate` → Claude returns:
   - API routes: `POST /apollo/contacts/search`, `POST /apollo/sequences/enroll`
   - Workflow templates: "Apollo Lead Enrich", "Apollo Sequence Trigger"

---

## Part 4 — Gap Summary & Checklist

### Already Done
- [x] Connector CRUD (6 HTTP endpoints)
- [x] OAuth2 + API Key + ConnectionString auth models
- [x] Azure Key Vault credential storage
- [x] Config-driven JSON per connector
- [x] `$secret` placeholder resolution at execution time
- [x] Connector linked to WorkflowStep via `ConnectorId`
- [x] `IAnthropicService` interface + implementation
- [x] Claude SDK registered in DI (`Anthropic.SDK` NuGet)

### Still To Build
- [ ] `TenantConnector` aggregate — **Table 1** (replaces hardcoded `ConnectorType` enum)
- [ ] `TenantConnectorApi` aggregate — **Table 2** (per-API-operation config, populated after validation)
- [ ] `ITenantConnectorRepository` + `SqlTenantConnectorRepository` (Table 1)
- [ ] `ITenantConnectorApiRepository` + `SqlTenantConnectorApiRepository` (Table 2)
- [ ] `TenantConnectors` + `TenantConnectorApis` DB tables + EF migration
- [ ] `ProvisionTenantConnectorCommand` — calls Claude to generate Metadata + Info
- [ ] `ValidateTenantConnectorCommand` — tests connection using Claude-generated `testEndpoint`
- [ ] `GenerateMcpWorkflowsCommand` — Claude with tools generates API routes + workflow defs
- [ ] `ListTenantConnectors` + `GetTenantConnector` queries
- [ ] Azure Functions HTTP triggers under `/tenants/{tenantId}/connectors`
- [ ] Unit tests (3 test classes + TenantConnectorBuilder)
- [ ] MCP tool layer (top of whiteboard)
- [ ] `get Search client` implementation (bottom of whiteboard)

---

## Part 5 — Files Reference

### Files to Modify

| File | Change |
|---|---|
| [WorkflowAIDbContext.cs](src/WorkflowAI.Infrastructure/Persistence/EntityFramework/WorkflowAIDbContext.cs) | Add `DbSet<TenantConnector>` |
| [DependencyInjection.cs](src/WorkflowAI.Infrastructure/DependencyInjection.cs) | Register `ITenantConnectorRepository` |

### Files to Create

| File | Purpose |
|---|---|
| `src/WorkflowAI.Domain/TenantConnectors/TenantId.cs` | Value object |
| `src/WorkflowAI.Domain/TenantConnectors/TenantConnectorId.cs` | Value object |
| `src/WorkflowAI.Domain/TenantConnectors/TenantConnectorStatus.cs` | Enumeration |
| `src/WorkflowAI.Domain/TenantConnectors/TenantConnector.cs` | Aggregate root |
| `src/WorkflowAI.Domain/TenantConnectors/ITenantConnectorRepository.cs` | Repository interface |
| `src/WorkflowAI.Domain/TenantConnectors/Events/TenantConnectorProvisionedEvent.cs` | Domain event |
| `src/WorkflowAI.Domain/TenantConnectors/Events/TenantConnectorActivatedEvent.cs` | Domain event |
| `src/WorkflowAI.Application/TenantConnectors/Commands/ProvisionTenantConnector/` | Command + Handler + Validator |
| `src/WorkflowAI.Application/TenantConnectors/Commands/ValidateTenantConnector/` | Command + Handler |
| `src/WorkflowAI.Application/TenantConnectors/Commands/GenerateMcpWorkflows/` | Command + Handler |
| `src/WorkflowAI.Application/TenantConnectors/Queries/` | List + Get queries |
| `src/WorkflowAI.Infrastructure/Persistence/EntityFramework/Configurations/TenantConnectorConfiguration.cs` | EF config |
| `src/WorkflowAI.Infrastructure/Persistence/EntityFramework/Repositories/SqlTenantConnectorRepository.cs` | EF repository |
| `src/WorkflowAI.Functions/TenantConnectors/TenantConnectorFunctions.cs` | HTTP endpoints |
| `tests/.../TenantConnectors/ProvisionTenantConnectorCommandHandlerTests.cs` | Unit tests |
| `tests/.../TenantConnectors/ValidateTenantConnectorCommandHandlerTests.cs` | Unit tests |
| `tests/.../TenantConnectors/GenerateMcpWorkflowsCommandHandlerTests.cs` | Unit tests |
| `tests/.../Common/Builders/TenantConnectorBuilder.cs` | Test builder |

### Reusable Patterns (don't reinvent)

| Pattern | Source |
|---|---|
| `AggregateRoot<TId>` | `src/WorkflowAI.Domain/Common/AggregateRoot.cs` |
| `Enumeration<T>` | `src/WorkflowAI.Domain/Common/Enumeration.cs` |
| `Result<T>` / `Error` | `src/WorkflowAI.Domain/Common/` |
| `IAnthropicService` | Already registered in DI |
| `ConnectorBuilder` (test) | Use as template for `TenantConnectorBuilder` |
| `SqlApolloEventRepository` | Use as template for `SqlTenantConnectorRepository` |
| `ApolloEventConfiguration` | Use as template for `TenantConnectorConfiguration` |

---

## Part 6 — Verification Checklist

1. `dotnet build` — no errors
2. `dotnet ef migrations add AddTenantConnectors` in Infrastructure project
3. Start Docker postgres + Azurite → `dotnet ef database update`
4. `dotnet test` — all unit tests pass
5. Smoke test:
   - `POST /tenants/{guid}/connectors` `{"connectorName":"Apollo"}` → returns Metadata + Info
   - `POST /tenants/{guid}/connectors/{id}/validate` with API key → status `Active`
   - `POST /tenants/{guid}/connectors/{id}/generate` → returns API routes + workflow defs
