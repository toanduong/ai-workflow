# Stage V1 — Workflow-AI Backend Skeleton

> **Date:** 2026-03-24 (updated)
> **Status:** ✅ Complete — Build succeeded, 10/10 tests passing
> **Docs Version:** 1.1 — Added Connector Management (OAuth, API Key, MSI, API Connections for Logic Apps)

---

## What Was Built

**Dynamic Workflow platform with AI Agent + Human-in-the-Loop approval pattern.**

### Architecture Decisions
- **Clean Architecture + DDD + CQRS + Event-Driven**
- **Presentation:** Azure Functions (isolated worker, .NET 10, `AzureFunctionsVersion=v4`)
- **CQRS:** MediatR 12.x with pipeline behaviors
- **Data:** Cosmos DB (workflows, executions, approvals) + Azure SQL via EF Core 10 (users, templates, channels)
- **Messaging:** Azure Service Bus (domain events → integration events)
- **AI:** Azure OpenAI (`Azure.AI.OpenAI 2.3.0-beta.1`)
- **Notifications:** Email (Azure Communication Services), Slack (Web API), Teams (Adaptive Cards)
- **Error Handling:** Result pattern (no exceptions for business logic), implicit `Error → Result` conversion
- **IDs:** Strongly-typed `readonly record struct` (WorkflowId, ExecutionId, etc.)
- **Enums:** Smart enum pattern via `Enumeration<T>` base class
- **Validation:** FluentValidation via MediatR `ValidationBehavior`
- **Mapping:** Mapster 10.x
- **Architecture Enforcement:** NetArchTest rules (6 tests)

---

## Solution Structure

```
WorkflowAI.slnx                          (.NET 10 slnx format)
├── Directory.Build.props                 (net10.0, nullable, TreatWarningsAsErrors, CPM)
├── Directory.Packages.props              (Central Package Management — all versions here)
├── global.json                           (SDK 10.0.200)
│
├── src/
│   ├── WorkflowAI.Domain/               → No deps except MediatR.Contracts
│   ├── WorkflowAI.Application/          → Domain
│   ├── WorkflowAI.Infrastructure/       → Application (transitive: Domain)
│   └── WorkflowAI.Functions/            → Application + Infrastructure
│
├── tests/
│   ├── WorkflowAI.Domain.UnitTests/           (4 tests ✅)
│   ├── WorkflowAI.Application.UnitTests/      (scaffold only)
│   ├── WorkflowAI.Infrastructure.IntTests/    (scaffold only)
│   ├── WorkflowAI.Functions.IntTests/         (scaffold only)
│   └── WorkflowAI.Architecture.Tests/         (6 tests ✅)
│
└── docs/
    ├── architecture/
    │   ├── C4-Architecture.md            (10 Mermaid diagrams: C4 L1-L3, ER, sequence, state, deployment)
    │   ├── Architecture-Documentation.md (16 sections: ADRs, security, observability, patterns)
    │   └── Implementation-Tasks.md       (8 phases, Gantt chart)
    └── stage_v1.md                       (this file)
```

---

## Domain Layer — Key Entities

| Aggregate/Entity | Folder | ID Type | Storage |
|---|---|---|---|
| `Workflow` + `WorkflowStep` | `Workflows/` | `WorkflowId` | Cosmos |
| `WorkflowExecution` + `StepExecution` | `Executions/` | `ExecutionId` | Cosmos |
| `ApprovalRequest` + `ApprovalAction` | `Approvals/` | `ApprovalRequestId` | Cosmos |
| `AIAgentTask` | `AIAgent/` | `AIAgentTaskId` | Cosmos |
| `Notification` | `Notifications/` | `NotificationId` | Cosmos |
| `User` | `Users/` | `UserId` | Azure SQL |
| `WorkflowTemplate` | `Templates/` | `TemplateId` | Azure SQL |
| `NotificationChannel` | `Channels/` | `ChannelId` | Azure SQL |
| `Connector` + `ConnectorCredential` | `Connectors/` | `ConnectorId` | Azure SQL |

### Domain Events (7)
`WorkflowCreated`, `WorkflowStarted`, `WorkflowCompleted`, `ExecutionStarted`, `StepCompleted`, `ExecutionCompleted`, `ApprovalRequested`, `ApprovalCompleted`, `ApprovalEscalated`

### Smart Enums (14)
`WorkflowStatus`, `StepType`, `TimeoutAction`, `ExecutionStatus`, `StepExecutionStatus`, `ApprovalStatus`, `ApprovalChannel`, `AITaskStatus`, `NotificationType`, `NotificationStatus`, `UserRole`, `ChannelType`, `ConnectorType`, `AuthModel`, `ConnectorStatus`, `CredentialType`

---

## Application Layer — CQRS Commands/Queries

### Implemented (Workflows — reference pattern)
| Type | Name | Returns |
|---|---|---|
| Command | `CreateWorkflowCommand` | `Result<WorkflowId>` |
| Command | `UpdateWorkflowCommand` | `Result` |
| Command | `DeleteWorkflowCommand` | `Result` |
| Command | `ExecuteWorkflowCommand` | `Result<ExecutionId>` |
| Query | `GetWorkflowQuery` | `Result<WorkflowDto>` |
| Query | `ListWorkflowsQuery` | `Result<IReadOnlyList<WorkflowSummaryDto>>` |
| Validator | `CreateWorkflowCommandValidator` | FluentValidation |
| EventHandler | `WorkflowCreatedEventHandler` | MediatR INotificationHandler |

### Pipeline Behaviors (order matters)
1. `UnhandledExceptionBehavior` — logs + rethrows
2. `ValidationBehavior` — FluentValidation → returns `Result.Failure`
3. `LoggingBehavior` — request/response logging
4. `PerformanceBehavior` — warns if >500ms

### Service Interfaces (12)
`IServiceBusPublisher`, `IDomainEventDispatcher`, `IAzureOpenAIService`, `INotificationSender`, `ILogicAppScriptGenerator`, `IBlobStorageService`, `ICurrentUserService`, `IDateTimeProvider`, `ITokenService`, `IConnectorService`, `IKeyVaultService`, `IApiConnectionProvisioner`

---

## Infrastructure Layer

| Component | Implementation | Notes |
|---|---|---|
| Cosmos repos (5) | `CosmosWorkflowRepository`, etc. | Partition keys: `/id`, `/workflowId`, `/stepExecutionId` |
| EF Core | `WorkflowAIDbContext` + 5 configs | Users, Templates, Channels, Connectors, ConnectorCredentials tables |
| Service Bus | `ServiceBusPublisher` | Topics: workflow-events, step-events, approval-events, notification-events |
| Domain Events | `DomainEventDispatcher` | MediatR in-process publish → clear events |
| AI | `AzureOpenAIService` | Chat completion + tool calling |
| Notifications | `EmailAdapter`, `SlackAdapter`, `TeamsAdapter` + `NotificationRouter` | Factory routing by channel type |
| Storage | `BlobStorageService` | For Logic App templates |
| Identity | `CurrentUserService`, `TokenService` | HMAC-SHA256 signed approval tokens |
| Connectors | `ConnectorService`, `KeyVaultService`, `ApiConnectionProvisioner` | OAuth flows, Key Vault secrets, ARM API Connection provisioning |
| DI | `AddInfrastructure(IConfiguration)` | Single extension method wires everything |

---

## Functions Layer (Presentation)

### HTTP Triggers (Route prefix: `api/`)
| Function | Route | Method |
|---|---|---|
| `CreateWorkflow` | `workflows` | POST |
| `GetWorkflow` | `workflows/{workflowId}` | GET |
| `ListWorkflows` | `workflows` | GET |
| `UpdateWorkflow` | `workflows/{workflowId}` | PUT |
| `DeleteWorkflow` | `workflows/{workflowId}` | DELETE |
| `ExecuteWorkflow` | `workflows/{workflowId}/execute` | POST |
| `GetPendingApprovals` | `approvals/pending` | GET |
| `TokenApproval` | `approvals/act?token=` | GET |
| `ListConnectors` | `connectors` | GET |
| `CreateConnector` | `connectors` | POST |
| `GetConnector` | `connectors/{connectorId}` | GET |
| `DeleteConnector` | `connectors/{connectorId}` | DELETE |
| `ValidateConnector` | `connectors/{connectorId}/validate` | POST |
| `OAuthCallback` | `connectors/oauth/callback` | POST |
| `SlackWebhook` | `webhooks/slack` | POST |
| `TeamsWebhook` | `webhooks/teams` | POST |
| `HealthCheck` | `health` | GET |

### Service Bus Triggers
| Function | Topic | Subscription |
|---|---|---|
| `WorkflowStartedHandler` | `workflow-events` | `ai-agent-processor` |
| `StepExecutionRequestedHandler` | `step-events` | `step-executor` |
| `ApprovalRequestedHandler` | `approval-events` | `notification-dispatcher` |
| `ApprovalCompletedHandler` | `approval-events` | `workflow-advancer` |
| `NotificationRequestedHandler` | `notification-events` | `channel-dispatcher` |

### Timer Triggers
| Function | Schedule | Purpose |
|---|---|---|
| `ApprovalTimeoutFunction` | Every 5 min | Check expired approvals |
| `ConnectorTokenRefreshFunction` | Every 5 min | Refresh OAuth tokens expiring within 15 min |
| `NotificationRetryFunction` | Every 10 min | Retry failed notifications |

---

## Known Build Notes

| Issue | Resolution |
|---|---|
| Cosmos DB Newtonsoft check | `AzureCosmosDisableNewtonsoftJsonCheck=true` in Directory.Build.props |
| Azure.AI.OpenAI no stable for net10 | Using `2.3.0-beta.1` |
| MediatR `RequestHandlerDelegate` | Does NOT accept `CancellationToken` arg — call `next()` not `next(ct)` |
| Azure Functions SDK + net10.0 | Works with SDK `2.0.7` + `AzureFunctionsVersion=v4` |
| Mapster version | Must use `10.0.0` (not 7.x) — `DependencyInjection` pkg requires it |
| Azure.Identity | Must use `1.14.2+` (EF Core SqlServer transitively requires it) |
| `Result` base class | Needs `implicit operator Result(Error)` for `return Error.xxx(...)` to compile |

---

## What's NOT Done Yet (TODO markers in code)

1. **Connector Management** — new feature: `Connector`, `ConnectorCredential` entities, OAuth flow, Key Vault integration, API Connection provisioning via ARM
2. **Connector CQRS** — `CreateConnector`, `CompleteOAuthFlow`, `ValidateConnector`, `DeleteConnector`, `RefreshConnectorToken` commands
3. **Logic App Generator + Connectors** — resolve connectors per step, embed `Microsoft.Web/connections` and `$connections` params in ARM templates
4. **Service Bus triggers** — have skeleton + deserialization, but TODO: dispatch to MediatR commands
5. **Webhook handlers** — Slack/Teams signature verification not implemented
6. **Approval timeout** — timer function exists but TODO: apply OnTimeoutAction policy
7. **Remaining CQRS** — only Workflows feature has commands/queries; Approvals, Executions, AIAgent, Notifications, Templates need their own
8. **EF Core migrations** — DbContext + configs exist, no initial migration created (now needs Connectors + ConnectorCredentials tables)
9. **Integration tests** — test projects scaffolded, no test cases yet
10. **Frontend** — not started (not in scope for backend skeleton)
