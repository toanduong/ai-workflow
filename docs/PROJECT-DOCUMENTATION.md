# WorkflowAI — Tài liệu Dự án Chi tiết

> **Version:** 1.0
> **Ngày cập nhật:** 2026-03-27
> **Tech Stack:** .NET 10 · Azure Functions v4 · Cosmos DB · Azure SQL · Service Bus · Azure OpenAI

---

## Mục lục

1. [Tổng quan dự án](#1-tổng-quan-dự-án)
2. [Kiến trúc tổng thể](#2-kiến-trúc-tổng-thể)
3. [Cấu trúc Solution](#3-cấu-trúc-solution)
4. [Domain Layer](#4-domain-layer)
5. [Application Layer](#5-application-layer)
6. [Infrastructure Layer](#6-infrastructure-layer)
7. [Functions Layer (Entry Point)](#7-functions-layer-entry-point)
8. [Luồng nghiệp vụ chính](#8-luồng-nghiệp-vụ-chính)
9. [Dữ liệu & Persistence](#9-dữ-liệu--persistence)
10. [Bảo mật](#10-bảo-mật)
11. [Cài đặt môi trường local](#11-cài-đặt-môi-trường-local)
12. [Cấu hình trong `local.settings.json`](#12-cấu-hình-trong-localsettingsjson)
13. [Các API Endpoint](#13-các-api-endpoint)
14. [Tests](#14-tests)
15. [Sơ đồ phụ thuộc](#15-sơ-đồ-phụ-thuộc)

---

## 1. Tổng quan dự án

**WorkflowAI** là một nền tảng tự động hóa workflow thông minh, cho phép người dùng doanh nghiệp thiết kế, thực thi và giám sát các luồng công việc có tích hợp AI Agent. Dự án triển khai pattern **Human-in-the-Loop (HITL)** — AI có thể xử lý bước tự động, nhưng các quyết định quan trọng vẫn cần con người phê duyệt trước khi tiếp tục.

### Khả năng chính

| Capability | Mô tả |
|---|---|
| **Workflow Designer** | Tạo và quản lý các workflow với nhiều bước (step) theo thứ tự |
| **AI Agent Execution** | Tích hợp Azure OpenAI để tự động hóa các bước xử lý nội dung/quyết định |
| **Human-in-the-Loop** | Bước phê duyệt (Approval) đa kênh: Web, Email, Slack, Teams |
| **Multi-channel Notifications** | Gửi thông báo qua Slack, Microsoft Teams, Email |
| **Connector Management** | Tạo và quản lý kết nối đến dịch vụ ngoài (OAuth2, API Key, MSI) |
| **Logic App Generation** | Tự động sinh ARM/Bicep script cho Azure Logic Apps từ định nghĩa workflow |
| **Approval Token** | Cơ chế phê duyệt qua link email có chữ ký (signed token), không cần đăng nhập |

---

## 2. Kiến trúc tổng thể

Dự án theo **Clean Architecture** kết hợp **Event-Driven Architecture** qua Azure Service Bus:

```
┌─────────────────────────────────────────────────────────────────┐
│                      Azure Functions (Host)                      │
│  ┌──────────────┐  ┌──────────────────┐  ┌───────────────────┐  │
│  │ HTTP Triggers │  │ ServiceBus Trigger│  │  Timer Triggers   │  │
│  │  (REST API)   │  │  (Event Handlers) │  │  (Scheduled Jobs) │  │
│  └──────┬───────┘  └────────┬─────────┘  └────────┬──────────┘  │
│         └──────────────────┼────────────────────── ┘             │
│                             ▼                                     │
│              Application Layer (MediatR / CQRS)                  │
│                    Commands & Queries                             │
│                             ▼                                     │
│                       Domain Layer                                │
│          (Entities, Aggregates, Domain Events)                    │
│                             ▼                                     │
│                  Infrastructure Layer                             │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────────┐    │
│  │ Cosmos DB│ │ Azure SQL│ │ServiceBus│ │ Azure OpenAI/ACS │    │
│  └──────────┘ └──────────┘ └──────────┘ └──────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

### Nguyên tắc kiến trúc

| # | Nguyên tắc | Lý do |
|---|---|---|
| 1 | **Clean Architecture** | Domain không phụ thuộc vào infrastructure |
| 2 | **CQRS** | Tách biệt Command (write) và Query (read) qua MediatR |
| 3 | **Event-Driven** | Domain Events → Integration Events → Service Bus |
| 4 | **Polyglot Persistence** | Cosmos DB cho documents, Azure SQL cho relational data |
| 5 | **API-First** | Toàn bộ tính năng expose qua REST API |
| 6 | **Security by Default** | Entra ID auth, Key Vault cho secrets, signed approval tokens |

---

## 3. Cấu trúc Solution

```
ai-workflow/
├── ai-workflow.sln
├── Directory.Build.props          # TargetFramework: net10.0, Nullable, TreatWarningsAsErrors
├── Directory.Packages.props       # Central Package Management
├── global.json                    # SDK: 10.0.200
│
├── src/
│   ├── WorkflowAI.Domain/         # Lớp Domain (Entities, Aggregates, Domain Events)
│   ├── WorkflowAI.Application/    # Lớp Application (Commands, Queries, Handlers)
│   ├── WorkflowAI.Infrastructure/ # Lớp Infrastructure (Persistence, Services, Messaging)
│   └── WorkflowAI.Functions/      # Entry Point (Azure Functions v4, HTTP/ServiceBus/Timer)
│
├── tests/
│   ├── WorkflowAI.Domain.UnitTests/
│   ├── WorkflowAI.Application.UnitTests/
│   ├── WorkflowAI.Infrastructure.IntegrationTests/
│   ├── WorkflowAI.Functions.IntegrationTests/
│   └── WorkflowAI.Architecture.Tests/   # Clean Architecture compliance tests
│
└── docs/
    ├── architecture/
    │   ├── Architecture-Documentation.md
    │   ├── C4-Architecture.md
    │   └── Implementation-Tasks.md
    └── setup.md
```

---

## 4. Domain Layer

**Project:** `WorkflowAI.Domain`

Lớp Domain là trung tâm của hệ thống, không phụ thuộc vào bất kỳ thư viện ngoài nào. Chứa toàn bộ business logic.

### 4.1 Aggregate Roots

#### `Workflow` — Workflow định nghĩa (template)

```
Workflow
├── Id: WorkflowId
├── Name: string
├── Description: string?
├── Status: Draft | Active | Archived
├── CreatedByUserId: UserId
├── TemplateId: Guid?
├── LogicAppResourceId: string?
└── Steps: List<WorkflowStep>
```

**Lifecycle:** `Draft` → (AddStep) → `Active` ← (Activate) | `Archived` ← (Archive)

**Domain Events:**
- `WorkflowCreatedEvent` — phát ra khi workflow được tạo

#### `WorkflowExecution` — Một lần thực thi workflow

```
WorkflowExecution
├── Id: ExecutionId
├── WorkflowId: WorkflowId
├── Status: Running | Completed | Failed | Cancelled | Paused
├── InputData: string? (JSON)
├── OutputData: string? (JSON)
├── StartedAt: DateTime
├── CompletedAt: DateTime?
├── TriggeredBy: string
└── Steps: List<StepExecution>
```

**Domain Events:**
- `ExecutionStartedEvent`
- `ExecutionCompletedEvent`

#### `ApprovalRequest` — Yêu cầu phê duyệt

```
ApprovalRequest
├── Id: ApprovalRequestId
├── StepExecutionId: Guid
├── Title: string
├── Description: string?
├── ContextData: string? (JSON hiển thị cho approver)
├── Status: Pending | Approved | Rejected | Escalated | TimedOut
├── ApprovalToken: string (signed JWT)
├── ExpiresAt: DateTime
└── Actions: List<ApprovalAction>
```

**Domain Events:**
- `ApprovalRequestedEvent` — gửi notification
- `ApprovalCompletedEvent` — tiếp tục execution

#### `Connector` — Kết nối đến dịch vụ ngoài

```
Connector
├── Id: ConnectorId
├── Name: string
├── ConnectorType: Office365 | ACS | Slack | Teams | SendGrid | Custom
├── AuthModel: APIKey | OAuth2 | ManagedIdentity
├── Status: Created | Active | Failed | Expired | Validating
├── CredentialId: Guid? (ref KeyVault)
├── AzureApiConnectionId: string?
├── ManagedApiId: string?
└── Configuration: string? (JSON)
```

### 4.2 Entities

| Entity | Thuộc aggregate | Mô tả |
|---|---|---|
| `WorkflowStep` | `Workflow` | Một bước trong workflow |
| `StepExecution` | `WorkflowExecution` | Trạng thái thực thi của một step |
| `ApprovalAction` | `ApprovalRequest` | Hành động phê duyệt/từ chối |

### 4.3 Enumerations (Smart Enum)

| Enum | Giá trị |
|---|---|
| `StepType` | `AIAgent`, `HumanApproval`, `Notification`, `Action` |
| `ConnectorType` | `Office365`, `ACS`, `Slack`, `Teams`, `SendGrid`, `Custom` |
| `AuthModel` | `APIKey`, `OAuth2`, `ManagedIdentity` |
| `ApprovalChannel` | `Web`, `Email`, `Slack`, `Teams` |
| `WorkflowStatus` | `Draft`, `Active`, `Archived` |
| `ExecutionStatus` | `Running`, `Completed`, `Failed`, `Cancelled`, `Paused` |
| `ApprovalStatus` | `Pending`, `Approved`, `Rejected`, `Escalated`, `TimedOut` |

---

## 5. Application Layer

**Project:** `WorkflowAI.Application`

Sử dụng pattern **CQRS** với **MediatR**. Toàn bộ business use-case được triển khai tại đây.

### 5.1 Workflows

| Command/Query | Mô tả |
|---|---|
| `CreateWorkflowCommand` | Tạo workflow mới (trạng thái Draft) |
| `UpdateWorkflowCommand` | Cập nhật name/description |
| `DeleteWorkflowCommand` | Xóa workflow |
| `ActivateWorkflowCommand` | Chuyển Draft → Active |
| `ExecuteWorkflowCommand` | Khởi tạo WorkflowExecution mới |
| `ListWorkflowsQuery` | Lấy danh sách workflows |
| `GetWorkflowQuery` | Lấy chi tiết một workflow |

### 5.2 Executions

| Command/Query | Mô tả |
|---|---|
| `ProcessStepExecutionCommand` | Xử lý thực thi một step (dispatch theo StepType) |
| `GetExecutionQuery` | Lấy chi tiết execution |

### 5.3 Approvals

| Command/Query | Mô tả |
|---|---|
| `CreateApprovalRequestCommand` | Tạo approval request cho HumanApproval step |
| `ProcessApprovalCommand` | Phê duyệt / từ chối (Approve/Reject) |
| `GetPendingApprovalsQuery` | Lấy danh sách approval đang chờ |

### 5.4 Connectors

| Command/Query | Mô tả |
|---|---|
| `CreateConnectorCommand` | Tạo connector mới |
| `DeleteConnectorCommand` | Xóa connector |
| `ValidateConnectorCommand` | Kiểm tra kết nối |
| `HandleOAuthCallbackCommand` | Xử lý OAuth2 callback |
| `GetConnectorQuery` | Lấy chi tiết connector |
| `ListConnectorsQuery` | Lấy danh sách connectors |

### 5.5 Notifications

| Command | Mô tả |
|---|---|
| `SendNotificationCommand` | Gửi notification theo channel |

### 5.6 Behaviors (Pipeline)

Các MediatR pipeline behaviors được đăng ký qua DI:

| Behavior | Mô tả |
|---|---|
| `ValidationBehavior<,>` | Chạy FluentValidation trước handler |
| `LoggingBehavior<,>` | Log request/response |

### 5.7 Domain Event Handlers

| Event Handler | Mô tả |
|---|---|
| `WorkflowStartedEventHandler` | Publish `WorkflowStarted` message lên Service Bus |
| Execution event handlers | Điều phối bước tiếp theo khi step hoàn thành |

---

## 6. Infrastructure Layer

**Project:** `WorkflowAI.Infrastructure`

### 6.1 Persistence

#### Cosmos DB (Document Store)

| Repository | Collection | Aggregate |
|---|---|---|
| `CosmosWorkflowRepository` | `workflows` | `Workflow` |
| `CosmosExecutionRepository` | `executions` | `WorkflowExecution` |
| `CosmosApprovalRepository` | `approvals` | `ApprovalRequest` |
| `CosmosNotificationRepository` | `notifications` | `Notification` |
| `CosmosAIAgentTaskRepository` | `ai-agent-tasks` | `AIAgentTask` |

#### Azure SQL (Relational Store — EF Core)

| Repository | Table | Entity |
|---|---|---|
| `SqlUserRepository` | `Users` | `User` |
| `SqlTemplateRepository` | `Templates` | `Template` |
| `SqlChannelRepository` | `Channels` | `Channel` |
| `SqlConnectorRepository` | `Connectors`, `ConnectorCredentials` | `Connector` |

### 6.2 Messaging

- **`ServiceBusPublisher`** — Publish domain events lên Azure Service Bus topics/queues
- **`DomainEventDispatcher`** — Dispatch domain events trong process trước khi publish externally

### 6.3 AI

- **`AzureOpenAIService`** — Gọi Azure OpenAI API để thực thi AI Agent steps
- **`AzureOpenAIOptions`** — Cấu hình Endpoint, ApiKey, DeploymentName

### 6.4 Notifications

| Adapter | Kênh | Protocol |
|---|---|---|
| `SlackNotificationAdapter` | Slack | Webhook (HttpClient) |
| `TeamsNotificationAdapter` | Microsoft Teams | Webhook (HttpClient) |
| `EmailNotificationAdapter` | Email | Azure Communication Services SDK |
| `NotificationRouter` | Router | Điều hướng đến đúng adapter theo channel |

### 6.5 Connectors & Logic Apps

- **`ConnectorService`** — CRUD và lifecycle management của connector
- **`ApiConnectionProvisioner`** — Provision Azure API Connections qua ARM REST API
- **`LogicAppGenerator`** — Sinh ARM/Bicep template cho Azure Logic Apps

### 6.6 Storage & Security

- **`BlobStorageService`** — Upload/Download file lên Azure Blob Storage
- **`KeyVaultService`** — Đọc/ghi secrets từ Azure Key Vault (credentials cho connectors)
- **`NoOpKeyVaultService`** — Stub dùng cho local development khi không có Key Vault URI
- **`TokenService`** — Tạo và verify signed JWT token cho approval links
- **`CurrentUserService`** — Lấy thông tin user từ HTTP context (Entra ID claims)

---

## 7. Functions Layer (Entry Point)

**Project:** `WorkflowAI.Functions`

Đây là entry point duy nhất của hệ thống, sử dụng **Azure Functions v4** với worker model **dotnet-isolated** trên `.NET 10`.

### 7.1 HTTP Triggers (REST API)

#### Workflows

| Function | Method | Route | Mô tả |
|---|---|---|---|
| `CreateWorkflowFunction` | POST | `/api/workflows` | Tạo workflow mới |
| `GetWorkflowFunction` | GET | `/api/workflows/{workflowId}` | Lấy chi tiết workflow |
| `ListWorkflowsFunction` | GET | `/api/workflows` | Danh sách workflows |
| `UpdateWorkflowFunction` | PUT | `/api/workflows/{workflowId}` | Cập nhật workflow |
| `DeleteWorkflowFunction` | DELETE | `/api/workflows/{workflowId}` | Xóa workflow |
| `ActivateWorkflowFunction` | POST | `/api/workflows/{workflowId}/activate` | Kích hoạt workflow |
| `ExecuteWorkflowFunction` | POST | `/api/workflows/{workflowId}/execute` | Chạy workflow |

#### Approvals

| Function | Method | Route | Mô tả |
|---|---|---|---|
| `GetPendingApprovals` | GET | `/api/approvals/pending` | Danh sách chờ duyệt |
| `TokenApproval` | GET | `/api/approvals/act` | Phê duyệt qua email link (token) |

#### Connectors

| Function | Method | Route | Mô tả |
|---|---|---|---|
| `CreateConnector` | POST | `/api/connectors` | Tạo connector |
| `GetConnector` | GET | `/api/connectors/{connectorId}` | Chi tiết connector |
| `ListConnectors` | GET | `/api/connectors` | Danh sách connectors |
| `DeleteConnector` | DELETE | `/api/connectors/{connectorId}` | Xóa connector |
| `ValidateConnector` | POST | `/api/connectors/{connectorId}/validate` | Kiểm tra kết nối |
| `OAuthCallback` | POST | `/api/connectors/oauth/callback` | OAuth2 callback |

#### Webhooks & Health

| Function | Method | Route | Mô tả |
|---|---|---|---|
| `SlackWebhook` | POST | `/api/webhooks/slack` | Nhận event từ Slack |
| `TeamsWebhook` | POST | `/api/webhooks/teams` | Nhận event từ Teams |
| `HealthCheck` | GET | `/api/health` | Health check endpoint |

### 7.2 Service Bus Triggers (Event Handlers)

| Function | Topic/Queue | Mô tả |
|---|---|---|
| `WorkflowStartedHandler` | `workflow-started` | Khởi chạy step đầu tiên khi workflow bắt đầu |
| `StepExecutionRequestedHandler` | `step-execution-requested` | Thực thi step theo StepType (AI/Approval/Notification/Action) |
| `ApprovalRequestedHandler` | `approval-requested` | Gửi notification phê duyệt đến approvers |
| `ApprovalCompletedHandler` | `approval-completed` | Tiếp tục workflow sau khi approval xong |
| `NotificationRequestedHandler` | `notification-requested` | Gửi notification qua adapter tương ứng |

### 7.3 Timer Triggers (Scheduled Jobs)

| Function | Schedule (CRON) | Mô tả |
|---|---|---|
| `CheckApprovalTimeouts` | Mỗi N phút | Kiểm tra approval quá hạn → Escalate hoặc Timeout |
| `RefreshConnectorTokens` | Mỗi giờ | Làm mới OAuth tokens sắp hết hạn |
| `RetryFailedNotifications` | Mỗi 15 phút | Retry gửi notification bị lỗi |

---

## 8. Luồng nghiệp vụ chính

### 8.1 Luồng thực thi Workflow đầy đủ

```
1. User gọi POST /api/workflows/{id}/execute
   └─► ExecuteWorkflowCommand
       └─► WorkflowExecution.Create() → ExecutionStartedEvent
           └─► Publish "workflow-started" lên Service Bus

2. WorkflowStartedHandler nhận event
   └─► Lấy step đầu tiên → Publish "step-execution-requested"

3. StepExecutionRequestedHandler nhận event
   ├─ StepType.AIAgent
   │   └─► AzureOpenAIService.GenerateAsync() → lưu kết quả → Publish "step-execution-requested" (step tiếp theo)
   │
   ├─ StepType.HumanApproval
   │   └─► ApprovalRequest.Create() → ApprovalRequestedEvent
   │       └─► Publish "approval-requested"
   │           └─► ApprovalRequestedHandler → gửi notification (Slack/Teams/Email)
   │                                       → Email có link chứa signed token
   │
   ├─ StepType.Notification
   │   └─► Publish "notification-requested" → gửi qua channel phù hợp
   │
   └─ StepType.Action
       └─► Gọi connector tương ứng → tiếp tục step tiếp theo

4. User phê duyệt:
   ├─ Qua Web/App: POST /api/approvals/{id}/approve
   ├─ Qua Email link: GET /api/approvals/act?token=...
   └─ Qua Slack/Teams: POST /api/webhooks/slack (hoặc /teams)
       └─► ApprovalRequest.Approve() → ApprovalCompletedEvent
           └─► Publish "approval-completed"
               └─► ApprovalCompletedHandler → chuyển sang step tiếp theo

5. Khi hết steps:
   └─► WorkflowExecution.Complete() → ExecutionCompletedEvent
       └─► Gửi output qua email đến stakeholders
```

### 8.2 Timeout Approval

```
CheckApprovalTimeouts (Timer, mỗi N phút)
└─► Tìm ApprovalRequest quá ExpiresAt
    ├─ TimeoutAction.Escalate → ApprovalRequest.Escalate() → gửi notification cấp cao hơn
    └─ TimeoutAction.Cancel   → ApprovalRequest.Timeout() → WorkflowExecution.Cancel()
```

### 8.3 Connector OAuth Flow

```
1. User tạo Connector (POST /api/connectors) với AuthModel = OAuth2
2. System trả về OAuth authorization URL
3. User redirect đến URL → đăng nhập → redirect về
   └─► POST /api/connectors/oauth/callback
       └─► Exchange code → access_token + refresh_token
           └─► Lưu vào Key Vault (ConnectorCredentials)
               └─► Connector.Activate()

4. RefreshConnectorTokens Timer → làm mới token trước khi hết hạn
```

---

## 9. Dữ liệu & Persistence

### 9.1 Azure Cosmos DB (NoSQL)

Dùng cho các entities có write-heavy, schema linh hoạt, hoặc cần scale cao:

| Container | Partition Key | Dữ liệu |
|---|---|---|
| `workflows` | `/id` | Workflow + Steps |
| `executions` | `/workflowId` | WorkflowExecution + StepExecutions |
| `approvals` | `/stepExecutionId` | ApprovalRequests + Actions |
| `notifications` | `/id` | Notification records |
| `ai-agent-tasks` | `/id` | AIAgentTask records |

**Database name:** `workflow-ai` (configurable qua `CosmosDb:DatabaseName`)

### 9.2 Azure SQL (Relational)

Dùng cho dữ liệu có quan hệ chặt chẽ, cần ACID, hoặc query phức tạp:

| Table | Mô tả |
|---|---|
| `Users` | User profiles, roles |
| `Templates` | Workflow templates có thể tái sử dụng |
| `Channels` | Notification channel config (Slack webhook URL, Teams URL, ...) |
| `Connectors` | Connector definition và lifecycle state |
| `ConnectorCredentials` | Key Vault secret references (không lưu credential thô) |

**ORM:** Entity Framework Core với migrations

---

## 10. Bảo mật

### 10.1 Authentication

- **Microsoft Entra ID (Azure AD)** — SSO/OAuth2 cho người dùng web
- Các HTTP triggers validate Bearer token từ Entra ID
- `CurrentUserService` extract `UserId` từ JWT claims

### 10.2 Approval Token

- Approval link trong email dùng **signed JWT** (HS256) với key cấu hình ở `Security:ApprovalTokenSigningKey`
- Token chứa: `approvalRequestId`, `action` (Approve/Reject), `expiresAt`
- Không yêu cầu approver phải đăng nhập — chỉ cần URL hợp lệ

### 10.3 Secrets Management

- **Azure Key Vault** lưu: connector credentials (OAuth tokens, API keys), certificates
- Application không bao giờ lưu credentials thô trong database — chỉ lưu `KeyVaultSecretName`
- `DefaultAzureCredential` — hỗ trợ Managed Identity trên Azure, local credential cho dev

### 10.4 Webhook Verification

- Slack webhook: verify `X-Slack-Signature` header (HMAC-SHA256)
- Teams webhook: verify Azure Bot Framework token

---

## 11. Cài đặt môi trường local

### Yêu cầu

| Công cụ | Version | Link |
|---|---|---|
| .NET SDK | 10.0.200+ | https://dot.net/v1/dotnet-install.sh |
| Azure Functions Core Tools | 4.6+ | `brew install azure-functions-core-tools@4` |
| Azurite | Latest | `brew install azurite` |
| Azure SQL / SQL Server | Local hoặc Azure | Docker hoặc Azure Free Tier |
| Azure Cosmos DB Emulator | Latest | Docker hoặc Azure Cosmos Emulator |

### Bước 1: Cài .NET 10

```bash
# Cài SDK (nếu chưa có)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --runtime dotnet

# Thêm vào PATH
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
```

### Bước 2: Build project

```bash
cd ai-workflow
dotnet build ai-workflow.sln --configuration Debug
```

### Bước 3: Khởi động Azurite (Azure Storage Emulator)

```bash
mkdir -p /tmp/azurite
azurite --location /tmp/azurite &
```

### Bước 4: Cấu hình `local.settings.json`

Cập nhật connection strings thật (xem mục 12).

### Bước 5: Chạy Azure Functions

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

cd src/WorkflowAI.Functions
func start
```

Functions sẽ listen tại `http://localhost:7071`.

### Bước 6: EF Core Migrations (Azure SQL)

```bash
cd src/WorkflowAI.Infrastructure

dotnet ef migrations add InitialCreate \
  --startup-project ../WorkflowAI.Functions \
  --context WorkflowAIDbContext

dotnet ef database update \
  --startup-project ../WorkflowAI.Functions \
  --context WorkflowAIDbContext
```

---

## 12. Cấu hình trong `local.settings.json`

File tại `src/WorkflowAI.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnection": "<Service Bus connection string>"
  },
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://<account>.documents.azure.com:443/;AccountKey=<key>;",
    "SqlDb": "Server=<server>;Database=workflow-ai;User Id=<user>;Password=<pw>;TrustServerCertificate=true;",
    "ServiceBus": "Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=...",
    "BlobStorage": "DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;",
    "AzureCommunicationServices": "endpoint=https://<acs>.communication.azure.com/;accesskey=<key>"
  },
  "AzureOpenAI": {
    "Endpoint": "https://<resource>.openai.azure.com/",
    "ApiKey": "<api-key>",
    "DeploymentName": "gpt-4"
  },
  "CosmosDb": {
    "DatabaseName": "workflow-ai"
  },
  "Security": {
    "ApprovalTokenSigningKey": "minimum-32-character-signing-key"
  },
  "KeyVault": {
    "Uri": "https://<vault-name>.vault.azure.net/"
  }
}
```

> **Lưu ý:** `local.settings.json` không được commit lên git (đã có trong `.gitignore`). Giữ bí mật toàn bộ keys/connection strings.

### Giải thích các key quan trọng

| Key | Bắt buộc | Mô tả |
|---|---|---|
| `AzureWebJobsStorage` | Có | `UseDevelopmentStorage=true` khi dùng Azurite local |
| `ServiceBusConnection` | Có | Connection string Service Bus cho các Service Bus Triggers |
| `CosmosDb` | Có | Connection string Azure Cosmos DB |
| `SqlDb` | Có | Connection string Azure SQL cho EF Core |
| `ServiceBus` | Có | Connection string Service Bus cho application publisher |
| `BlobStorage` | Có | Lưu generated Logic App scripts |
| `AzureCommunicationServices` | Tùy chọn | Cần khi dùng Email notifications |
| `AzureOpenAI.*` | Tùy chọn | Cần khi có AIAgent steps |
| `Security:ApprovalTokenSigningKey` | Có | Key ký JWT cho approval email links (≥32 ký tự) |
| `KeyVault:Uri` | Tùy chọn | Nếu bỏ trống → dùng `NoOpKeyVaultService` (không lưu được secrets) |

---

## 13. Các API Endpoint

Base URL: `http://localhost:7071` (local) / `https://<function-app>.azurewebsites.net` (production)

### Workflows

```
GET    /api/workflows                          # Danh sách workflows
POST   /api/workflows                          # Tạo workflow
GET    /api/workflows/{workflowId}             # Chi tiết workflow
PUT    /api/workflows/{workflowId}             # Cập nhật workflow
DELETE /api/workflows/{workflowId}             # Xóa workflow
POST   /api/workflows/{workflowId}/activate    # Kích hoạt (Draft → Active)
POST   /api/workflows/{workflowId}/execute     # Chạy workflow
```

### Approvals

```
GET    /api/approvals/pending                  # Danh sách chờ duyệt
GET    /api/approvals/act?token=...            # Phê duyệt qua email link
```

### Connectors

```
GET    /api/connectors                         # Danh sách connectors
POST   /api/connectors                         # Tạo connector
GET    /api/connectors/{connectorId}           # Chi tiết connector
DELETE /api/connectors/{connectorId}           # Xóa connector
POST   /api/connectors/{connectorId}/validate  # Kiểm tra kết nối
POST   /api/connectors/oauth/callback          # OAuth2 callback
```

### Webhooks & Health

```
POST   /api/webhooks/slack                     # Slack event webhook
POST   /api/webhooks/teams                     # Teams event webhook
GET    /api/health                             # Health check
```

---

## 14. Tests

### Cấu trúc Tests

| Project | Loại | Mô tả |
|---|---|---|
| `WorkflowAI.Domain.UnitTests` | Unit | Test domain entities, business rules |
| `WorkflowAI.Application.UnitTests` | Unit | Test command/query handlers với mock |
| `WorkflowAI.Infrastructure.IntegrationTests` | Integration | Test repositories, external services |
| `WorkflowAI.Functions.IntegrationTests` | Integration | Test HTTP triggers end-to-end |
| `WorkflowAI.Architecture.Tests` | Architecture | Kiểm tra Clean Architecture constraints |

### Chạy tests

```bash
# Tất cả tests
dotnet test ai-workflow.sln

# Chỉ unit tests
dotnet test tests/WorkflowAI.Domain.UnitTests
dotnet test tests/WorkflowAI.Application.UnitTests

# Architecture tests (kiểm tra dependency rules)
dotnet test tests/WorkflowAI.Architecture.Tests
```

### Architecture Tests

`WorkflowAI.Architecture.Tests` dùng thư viện NetArchTest để kiểm tra:
- Domain không reference Infrastructure
- Application không reference Infrastructure
- Infrastructure không reference Functions
- Tất cả Entities đều trong namespace `*.Domain.*`

---

## 15. Sơ đồ phụ thuộc

```
WorkflowAI.Functions
    ├── WorkflowAI.Application
    │       ├── WorkflowAI.Domain        (không có external dependency)
    │       └── [MediatR, FluentValidation]
    └── WorkflowAI.Infrastructure
            ├── WorkflowAI.Application   (chỉ interfaces)
            ├── WorkflowAI.Domain
            └── [EF Core, Cosmos SDK, Azure Service Bus, Azure OpenAI, ...]
```

**Nguyên tắc dependency:**
- `Domain` → không phụ thuộc gì
- `Application` → chỉ phụ thuộc `Domain` + abstractions
- `Infrastructure` → implement interfaces từ `Application`, phụ thuộc Azure SDKs
- `Functions` → wire up tất cả, phụ thuộc `Application` + `Infrastructure`

---

*Document này được tạo dựa trên source code tại ngày 2026-03-27.*
