# Workflow-AI: Dynamic Workflow with AI Agent & Human-in-the-Loop

## C4 Architecture Documentation

> **Tech Stack:** .NET 10, Azure Services (Logic Apps, Service Bus, Functions, Cosmos DB, PostgreSQL, Communication Services)
> **Pattern:** AI Agent with Human-in-the-Loop Approval

---

## 1. System Context Diagram (C4 Level 1)

The highest-level view showing the Workflow-AI system and its interactions with external actors and systems.

```mermaid
C4Context
    title System Context Diagram - Workflow-AI Platform

    Person(user, "Business User", "Creates workflows, reviews & approves AI-generated tasks")
    Person(admin, "Admin", "Configures workflow templates, notification channels, and policies")

    System(workflowAI, "Workflow-AI Platform", "Dynamic workflow engine with AI agent orchestration and human-in-the-loop approval")

    System_Ext(azureOpenAI, "Azure OpenAI Service", "AI/LLM for intelligent task generation, decision support, and content generation")
    System_Ext(slackAPI, "Slack API", "Sends approval requests and notifications to Slack channels")
    System_Ext(teamsAPI, "Microsoft Teams", "Sends adaptive cards for approval and notifications")
    System_Ext(emailService, "Azure Communication Services (Email)", "Sends email notifications and approval links")
    System_Ext(azureAD, "Microsoft Entra ID", "Identity & access management, SSO authentication")
    System_Ext(azureARM, "Azure Resource Manager", "Provisions API Connection resources for Logic App connectors")
    System_Ext(keyVault, "Azure Key Vault", "Stores connector credentials, secrets, and tokens securely")

    Rel(user, workflowAI, "Creates workflows, approves/rejects tasks", "HTTPS / Browser")
    Rel(admin, workflowAI, "Configures templates, channels & connectors", "HTTPS / Browser")
    Rel(workflowAI, azureOpenAI, "Generates tasks, analyzes data", "HTTPS / REST API")
    Rel(workflowAI, slackAPI, "Sends notifications & approval requests", "HTTPS / Webhook")
    Rel(workflowAI, teamsAPI, "Sends adaptive cards & notifications", "HTTPS / Graph API")
    Rel(workflowAI, emailService, "Sends approval emails & output notifications", "HTTPS / REST API")
    Rel(workflowAI, azureAD, "Authenticates users", "OAuth 2.0 / OIDC")
    Rel(workflowAI, azureARM, "Provisions API Connections", "HTTPS / ARM REST API")
    Rel(workflowAI, keyVault, "Reads/Writes connector secrets", "HTTPS / SDK")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

### Context Summary

| Actor / System | Role | Integration |
|---|---|---|
| **Business User** | Initiates workflows, approves/rejects AI-generated steps | Web UI, Email links, Slack/Teams actions |
| **Admin** | Configures templates, notification channels, connectors, policies | Web UI |
| **Azure Resource Manager** | Provisions API Connection resources for Logic App connectors | ARM REST API |
| **Azure OpenAI** | AI backbone for task generation and decision support | REST API |
| **Slack** | Notification & approval channel | Webhooks / Slack API |
| **Microsoft Teams** | Notification & approval channel | Graph API / Adaptive Cards |
| **Email (ACS)** | Output delivery & approval notifications | Azure Communication Services |
| **Entra ID** | Identity provider | OAuth 2.0 / OIDC |

---

## 2. Container Diagram (C4 Level 2)

Shows the high-level technology choices and how containers interact within the Workflow-AI system.

```mermaid
C4Container
    title Container Diagram - Workflow-AI Platform

    Person(user, "Business User", "Approves tasks, monitors workflows")

    System_Boundary(workflowPlatform, "Workflow-AI Platform") {
        Container(spa, "Frontend SPA", "React / Angular", "Workflow designer, dashboard, approval UI")
        Container(apiGateway, "API Gateway", ".NET 10 / YARP", "Routes requests, rate limiting, auth validation")
        Container(workflowAPI, "Workflow API", ".NET 10 Web API", "Workflow CRUD, execution control, user management")
        Container(aiAgentService, "AI Agent Service", ".NET 10 Worker", "Orchestrates AI agent tasks, prompt engineering, tool calling")
        Container(approvalEngine, "Approval Engine", ".NET 10 Worker", "Manages human-in-the-loop approval lifecycle")
        Container(notificationService, "Notification Service", ".NET 10 Worker", "Multi-channel notification dispatch (Slack, Teams, Email)")
        Container(logicAppGenerator, "Logic App Generator", ".NET 10 Service", "Generates & deploys Azure Logic App ARM/Bicep scripts with API Connections")
        Container(connectorManager, "Connector Manager", ".NET 10 Service", "Manages connector lifecycle: OAuth flows, API key storage, API Connection provisioning")
        ContainerDb(cosmosDB, "Cosmos DB", "Azure Cosmos DB (NoSQL)", "Tasks, approvals, audit logs")
        ContainerDb(sqlDB, "PostgreSQL", "Azure Database for PostgreSQL", "User profiles, templates, channel configs, connectors")
        Container(serviceBus, "Service Bus", "Azure Service Bus", "Async messaging between services (events, commands)")
        Container(blobStorage, "Blob Storage", "Azure Blob Storage", "Generated Logic App scripts, attachments, exports")
        Container(keyVault, "Key Vault", "Azure Key Vault", "Connector credentials, API keys, secrets, tokens")
    }

    System_Ext(azureOpenAI, "Azure OpenAI", "LLM for AI agent")
    System_Ext(slackAPI, "Slack API", "Notifications")
    System_Ext(teamsAPI, "MS Teams", "Notifications")
    System_Ext(emailACS, "Azure Communication Services", "Email")
    System_Ext(azureARM, "Azure Resource Manager", "API Connection provisioning")

    Rel(user, spa, "Uses", "HTTPS")
    Rel(spa, apiGateway, "API calls", "HTTPS/JSON")
    Rel(apiGateway, workflowAPI, "Routes to", "HTTPS")
    Rel(workflowAPI, cosmosDB, "Reads/Writes workflows", "SDK")
    Rel(workflowAPI, sqlDB, "Reads/Writes config", "EF Core")
    Rel(workflowAPI, serviceBus, "Publishes events", "AMQP")
    Rel(workflowAPI, logicAppGenerator, "Requests script generation", "HTTPS")
    Rel(aiAgentService, azureOpenAI, "Calls LLM", "HTTPS")
    Rel(aiAgentService, serviceBus, "Consumes/Publishes", "AMQP")
    Rel(aiAgentService, cosmosDB, "Reads/Writes tasks", "SDK")
    Rel(approvalEngine, serviceBus, "Consumes approval events", "AMQP")
    Rel(approvalEngine, cosmosDB, "Reads/Writes approvals", "SDK")
    Rel(approvalEngine, notificationService, "Triggers notifications", "Service Bus")
    Rel(notificationService, slackAPI, "Sends", "HTTPS")
    Rel(notificationService, teamsAPI, "Sends", "HTTPS")
    Rel(notificationService, emailACS, "Sends", "HTTPS")
    Rel(logicAppGenerator, blobStorage, "Stores scripts", "SDK")
    Rel(logicAppGenerator, connectorManager, "Resolves connectors for steps", "Internal")
    Rel(connectorManager, keyVault, "Reads/Writes secrets", "SDK")
    Rel(connectorManager, sqlDB, "Reads/Writes connector configs", "EF Core")
    Rel(connectorManager, azureARM, "Provisions API Connection resources", "HTTPS")
    Rel(notificationService, keyVault, "Reads channel credentials", "SDK")

    UpdateLayoutConfig($c4ShapeInRow="4", $c4BoundaryInRow="1")
```

### Container Summary

| Container | Technology | Purpose |
|---|---|---|
| **Frontend SPA** | React or Angular | Workflow designer, approval UI, dashboards |
| **API Gateway** | .NET 10 / YARP | Request routing, authentication, rate limiting |
| **Workflow API** | .NET 10 Web API | Core CRUD, workflow execution orchestration |
| **AI Agent Service** | .NET 10 Worker Service | AI task orchestration, prompt management, tool calling |
| **Approval Engine** | .NET 10 Worker Service | Human-in-the-loop approval lifecycle |
| **Notification Service** | .NET 10 Worker Service | Multi-channel notification dispatch |
| **Logic App Generator** | .NET 10 Service | Generates ARM/Bicep templates for Azure Logic Apps with API Connections |
| **Connector Manager** | .NET 10 Service | Manages connector lifecycle: OAuth flows, credentials, API Connection provisioning |
| **Cosmos DB** | Azure Cosmos DB | Tasks, approvals, audit trail |
| **PostgreSQL** | Azure Database for PostgreSQL | User profiles, templates, channel configurations, connectors |
| **Key Vault** | Azure Key Vault | Connector credentials, API keys, signing secrets |
| **Service Bus** | Azure Service Bus | Event-driven async communication |
| **Blob Storage** | Azure Blob Storage | Generated scripts, attachments |

---

## 3. Component Diagram (C4 Level 3) — Workflow API

```mermaid
C4Component
    title Component Diagram - Workflow API

    Container_Boundary(api, "Workflow API (.NET 10)") {
        Component(workflowController, "WorkflowController", "API Controller", "REST endpoints for workflow CRUD & execution")
        Component(approvalController, "ApprovalController", "API Controller", "REST endpoints for approval actions (approve/reject)")
        Component(templateController, "TemplateController", "API Controller", "Manage workflow templates")
        Component(connectorController, "ConnectorController", "API Controller", "Connector CRUD, OAuth callback, validation, provisioning")
        Component(workflowService, "WorkflowService", "Domain Service", "Workflow orchestration & state machine logic")
        Component(approvalService, "ApprovalService", "Domain Service", "Approval lifecycle, escalation, timeout handling")
        Component(aiOrchestrator, "AIOrchestrationService", "Domain Service", "Coordinates AI agent execution & tool calling")
        Component(scriptGenerator, "ScriptGeneratorService", "Domain Service", "Generates Logic App JSON/Bicep definitions with API Connections")
        Component(connectorService, "ConnectorService", "Domain Service", "Connector lifecycle: OAuth exchange, credential storage, API Connection provisioning")
        Component(eventPublisher, "EventPublisher", "Infrastructure", "Publishes domain events to Service Bus")
        Component(cosmosRepo, "CosmosRepository", "Infrastructure", "Data access for Cosmos DB")
        Component(sqlRepo, "SqlRepository", "Infrastructure", "Data access for PostgreSQL via EF Core")
        Component(keyVaultClient, "KeyVaultClient", "Infrastructure", "Reads/Writes secrets to Azure Key Vault")
        Component(armClient, "ArmClient", "Infrastructure", "Provisions Azure API Connection resources via ARM")
    }

    ContainerDb(cosmosDB, "Cosmos DB", "Tasks, Approvals")
    ContainerDb(sqlDB, "PostgreSQL", "Users, Templates, Configs, Connectors")
    Container(serviceBus, "Service Bus", "Event messaging")
    Container(keyVault, "Key Vault", "Secrets")
    System_Ext(azureOpenAI, "Azure OpenAI", "LLM")
    System_Ext(azureARM, "Azure Resource Manager", "API Connection provisioning")

    Rel(workflowController, workflowService, "Uses")
    Rel(approvalController, approvalService, "Uses")
    Rel(templateController, sqlRepo, "Uses")
    Rel(connectorController, connectorService, "Uses")
    Rel(workflowService, aiOrchestrator, "Triggers AI agent")
    Rel(workflowService, scriptGenerator, "Generates scripts")
    Rel(workflowService, eventPublisher, "Publishes events")
    Rel(workflowService, cosmosRepo, "Persists workflows")
    Rel(approvalService, eventPublisher, "Publishes approval events")
    Rel(approvalService, cosmosRepo, "Persists approvals")
    Rel(connectorService, sqlRepo, "Persists connector configs")
    Rel(connectorService, keyVaultClient, "Stores/retrieves secrets")
    Rel(connectorService, armClient, "Provisions API Connections")
    Rel(scriptGenerator, connectorService, "Resolves connectors for steps")
    Rel(aiOrchestrator, azureOpenAI, "Calls LLM", "HTTPS")
    Rel(eventPublisher, serviceBus, "Sends messages", "AMQP")
    Rel(cosmosRepo, cosmosDB, "Reads/Writes", "SDK")
    Rel(sqlRepo, sqlDB, "Reads/Writes", "EF Core")
    Rel(keyVaultClient, keyVault, "Reads/Writes", "SDK")
    Rel(armClient, azureARM, "Provisions resources", "HTTPS")
```

---

## 4. Component Diagram (C4 Level 3) — Notification Service

```mermaid
C4Component
    title Component Diagram - Notification Service

    Container_Boundary(notifSvc, "Notification Service (.NET 10 Worker)") {
        Component(eventConsumer, "EventConsumer", "Service Bus Consumer", "Listens for notification events from Service Bus")
        Component(notifRouter, "NotificationRouter", "Domain Service", "Routes notifications to configured channels")
        Component(credentialResolver, "CredentialResolver", "Domain Service", "Resolves channel credentials from Connector config + Key Vault")
        Component(templateEngine, "TemplateEngine", "Domain Service", "Renders notification templates (email body, Slack blocks, adaptive cards)")
        Component(slackAdapter, "SlackAdapter", "Channel Adapter", "Sends messages via Slack API with action buttons")
        Component(teamsAdapter, "TeamsAdapter", "Channel Adapter", "Sends adaptive cards via MS Graph API")
        Component(emailAdapter, "EmailAdapter", "Channel Adapter", "Sends emails via Azure Communication Services")
        Component(deliveryTracker, "DeliveryTracker", "Infrastructure", "Tracks delivery status & retry logic")
    }

    Container(serviceBus, "Service Bus", "Event source")
    Container(keyVault, "Key Vault", "Channel credentials")
    ContainerDb(sqlDB, "PostgreSQL", "Connector configs")
    System_Ext(slackAPI, "Slack API", "Slack workspace")
    System_Ext(teamsAPI, "MS Teams / Graph API", "Teams channels")
    System_Ext(emailACS, "Azure Communication Services", "Email delivery")
    ContainerDb(cosmosDB, "Cosmos DB", "Delivery logs")

    Rel(eventConsumer, serviceBus, "Consumes", "AMQP")
    Rel(eventConsumer, notifRouter, "Forwards events")
    Rel(notifRouter, credentialResolver, "Resolves credentials")
    Rel(credentialResolver, sqlDB, "Loads connector config", "EF Core")
    Rel(credentialResolver, keyVault, "Retrieves secrets", "SDK")
    Rel(notifRouter, templateEngine, "Renders content")
    Rel(notifRouter, slackAdapter, "Dispatches to Slack")
    Rel(notifRouter, teamsAdapter, "Dispatches to Teams")
    Rel(notifRouter, emailAdapter, "Dispatches to Email")
    Rel(slackAdapter, slackAPI, "Sends", "HTTPS")
    Rel(teamsAdapter, teamsAPI, "Sends", "HTTPS")
    Rel(emailAdapter, emailACS, "Sends", "HTTPS")
    Rel(deliveryTracker, cosmosDB, "Logs delivery", "SDK")
```

---

## 5. Data Model (Entity Relationship Diagram)

This data model serves the frontend and drives the workflow engine.

```mermaid
erDiagram
    Workflow ||--o{ WorkflowStep : contains
    Workflow ||--o{ WorkflowExecution : "has executions"
    Workflow }o--|| WorkflowTemplate : "based on"
    WorkflowExecution ||--o{ StepExecution : contains
    WorkflowStep ||--o{ StepExecution : "executed as"
    WorkflowStep }o--o| Connector : "may use"
    StepExecution ||--o{ ApprovalRequest : "may require"
    ApprovalRequest ||--o{ ApprovalAction : "has actions"
    ApprovalRequest ||--o{ Notification : triggers
    StepExecution ||--o{ AIAgentTask : "may generate"
    Notification ||--|| NotificationChannel : "sent via"
    NotificationChannel ||--|| Connector : "authenticated by"
    Connector ||--|| ConnectorCredential : "has credential"
    Workflow }o--|| User : "created by"
    ApprovalAction }o--|| User : "acted by"

    Workflow {
        guid Id PK
        string Name
        string Description
        string Status "Draft|Active|Archived"
        guid CreatedByUserId FK
        guid TemplateId FK
        datetime CreatedAt
        datetime UpdatedAt
        string LogicAppResourceId "Azure Resource ID"
    }

    WorkflowTemplate {
        guid Id PK
        string Name
        string Description
        string Category
        json DefaultSteps "JSON step definitions"
        bool IsActive
        datetime CreatedAt
    }

    WorkflowStep {
        guid Id PK
        guid WorkflowId FK
        int OrderIndex
        string Name
        string StepType "AIAgent|HumanApproval|Notification|Action"
        json Configuration "Step-specific config JSON"
        guid ConnectorId FK "Optional: connector for this step"
        string RequiredRole "Role needed to approve"
        int TimeoutMinutes
        string OnTimeoutAction "Escalate|AutoApprove|AutoReject"
    }

    WorkflowExecution {
        guid Id PK
        guid WorkflowId FK
        string Status "Running|Paused|Completed|Failed|Cancelled"
        json InputData
        json OutputData
        datetime StartedAt
        datetime CompletedAt
        string TriggeredBy "Manual|Scheduled|API|LogicApp"
    }

    StepExecution {
        guid Id PK
        guid WorkflowExecutionId FK
        guid WorkflowStepId FK
        string Status "Pending|InProgress|WaitingApproval|Approved|Rejected|Completed|Failed|Skipped"
        json InputData
        json OutputData
        datetime StartedAt
        datetime CompletedAt
        string ErrorMessage
    }

    ApprovalRequest {
        guid Id PK
        guid StepExecutionId FK
        string Title
        string Description
        json ContextData "Data for reviewer to evaluate"
        string Status "Pending|Approved|Rejected|Escalated|TimedOut"
        string ApprovalToken "Unique token for email/link approval"
        datetime ExpiresAt
        datetime CreatedAt
    }

    ApprovalAction {
        guid Id PK
        guid ApprovalRequestId FK
        guid UserId FK
        string Action "Approve|Reject|Escalate|Comment"
        string Comment
        string Channel "Web|Email|Slack|Teams"
        datetime ActedAt
    }

    AIAgentTask {
        guid Id PK
        guid StepExecutionId FK
        string PromptTemplate
        json PromptVariables
        json LLMResponse
        string Model "gpt-4o|gpt-4o-mini"
        int TokensUsed
        string Status "Pending|Processing|Completed|Failed"
        datetime CreatedAt
        datetime CompletedAt
    }

    Notification {
        guid Id PK
        guid ApprovalRequestId FK
        guid ChannelId FK
        string Type "ApprovalRequest|ApprovalResult|WorkflowComplete|Error"
        string Subject
        string Body
        string Status "Queued|Sent|Delivered|Failed"
        string RecipientAddress
        int RetryCount
        datetime SentAt
        datetime DeliveredAt
    }

    NotificationChannel {
        guid Id PK
        string Name
        string ChannelType "Email|Slack|Teams"
        guid ConnectorId FK "Links to Connector for authentication"
        bool IsActive
        datetime CreatedAt
    }

    Connector {
        guid Id PK
        string Name "e.g. Office365-Prod, Slack-Marketing"
        string ConnectorType "Office365|ACS|Slack|Teams|SendGrid|Custom"
        string AuthModel "OAuth2|ServicePrincipal|APIKey|ManagedIdentity"
        string Status "Created|Validating|Active|Failed|Expired"
        guid CredentialId FK
        string AzureApiConnectionId "Azure resource ID (for Logic Apps)"
        string ManagedApiId "e.g. Microsoft.Web/locations/managedApis/office365"
        json Configuration "Non-secret config (sender address, tenant, etc.)"
        guid CreatedByUserId FK
        datetime CreatedAt
        datetime UpdatedAt
        datetime ExpiresAt "OAuth token expiry"
    }

    ConnectorCredential {
        guid Id PK
        guid ConnectorId FK
        string KeyVaultSecretName "Reference to Key Vault secret (never stores raw secret)"
        string KeyVaultSecretVersion
        string CredentialType "AccessToken|RefreshToken|APIKey|ConnectionString|ClientSecret"
        datetime IssuedAt
        datetime ExpiresAt
        datetime LastRotatedAt
    }

    User {
        guid Id PK
        string ExternalId "Entra ID Object ID"
        string DisplayName
        string Email
        string Role "Admin|Approver|Viewer"
        json NotificationPreferences
        datetime CreatedAt
    }
```

### Key Data Model Concepts

| Entity | Purpose | Storage |
|---|---|---|
| **Workflow** | Definition of a workflow with its steps | Blob Storage |
| **WorkflowTemplate** | Reusable workflow blueprints | PostgreSQL |
| **WorkflowStep** | Individual step definition (AI, Approval, Notification) | Blob Storage |
| **WorkflowExecution** | Runtime instance of a workflow | Cosmos DB |
| **StepExecution** | Runtime state of each step | Cosmos DB |
| **ApprovalRequest** | Human-in-the-loop approval with token-based access | Cosmos DB |
| **ApprovalAction** | Audit trail of approval decisions | Cosmos DB |
| **AIAgentTask** | AI/LLM execution record with token tracking | Cosmos DB |
| **Notification** | Notification delivery record with retry tracking | Cosmos DB |
| **NotificationChannel** | Channel configuration linked to Connector for auth | PostgreSQL |
| **Connector** | Connector definition (type, auth model, Azure API Connection reference) | PostgreSQL |
| **ConnectorCredential** | Key Vault secret pointer (never stores raw secrets) | PostgreSQL |
| **User** | User profile linked to Entra ID | PostgreSQL |

---

## 6. Sequence Diagram — Workflow Execution with Human-in-the-Loop

```mermaid
sequenceDiagram
    actor User as Business User
    participant SPA as Frontend SPA
    participant API as Workflow API
    participant AI as AI Agent Service
    participant LLM as Azure OpenAI
    participant DB as Cosmos DB
    participant Bus as Service Bus
    participant Approval as Approval Engine
    participant Notif as Notification Service
    participant Email as Email (ACS)
    participant Slack as Slack
    participant Teams as MS Teams

    User->>SPA: Start workflow execution
    SPA->>API: POST /api/workflows/{id}/execute
    API->>DB: Create WorkflowExecution (Status: Running)
    API->>Bus: Publish WorkflowStarted event
    API-->>SPA: 202 Accepted (executionId)

    Note over AI,LLM: Step 1: AI Agent generates content/decisions
    Bus->>AI: WorkflowStarted event
    AI->>DB: Create StepExecution (Status: InProgress)
    AI->>LLM: Send prompt with context & tools
    LLM-->>AI: Generated response + tool calls
    AI->>DB: Save AIAgentTask (response, tokens)
    AI->>DB: Update StepExecution (Status: Completed, OutputData)
    AI->>Bus: Publish StepCompleted event

    Note over Approval,Notif: Step 2: Human-in-the-Loop Approval
    Bus->>Approval: StepCompleted → next step requires approval
    Approval->>DB: Create ApprovalRequest (Status: Pending, Token)
    Approval->>Bus: Publish ApprovalRequested event

    par Send notifications to all channels
        Bus->>Notif: ApprovalRequested event
        Notif->>Email: Send approval email with action link
        Notif->>Slack: Send message with Approve/Reject buttons
        Notif->>Teams: Send adaptive card with actions
    end

    alt User approves via Email link
        User->>API: GET /api/approvals/act?token={token}&action=approve
        API->>Approval: Process approval
    else User approves via Slack
        User->>Slack: Clicks "Approve" button
        Slack->>API: POST /api/webhooks/slack (interaction payload)
        API->>Approval: Process approval
    else User approves via Teams
        User->>Teams: Clicks "Approve" on adaptive card
        Teams->>API: POST /api/webhooks/teams (action payload)
        API->>Approval: Process approval
    end

    Approval->>DB: Create ApprovalAction (Approved)
    Approval->>DB: Update ApprovalRequest (Status: Approved)
    Approval->>DB: Update StepExecution (Status: Approved)
    Approval->>Bus: Publish ApprovalCompleted event

    Note over Notif,Email: Step 3: Output Notification via Email
    Bus->>Notif: WorkflowCompleted event
    Notif->>DB: Create Notification (Type: WorkflowComplete)
    Notif->>Email: Send output/results email to stakeholders
    Email-->>User: 📧 Workflow results delivered
```

---

## 7. Sequence Diagram — Logic App Script Generation

```mermaid
sequenceDiagram
    actor Admin as Admin User
    participant SPA as Frontend SPA
    participant API as Workflow API
    participant ConnMgr as Connector Manager
    participant KV as Key Vault
    participant Gen as Logic App Generator
    participant AI as AI Agent Service
    participant LLM as Azure OpenAI
    participant Blob as Blob Storage
    participant ARM as Azure Resource Manager

    Note over Admin,ARM: Pre-requisite: Admin configures connectors

    alt OAuth connector (Office 365, Teams)
        Admin->>SPA: Add Office 365 connector
        SPA->>API: POST /api/connectors (type: Office365, auth: OAuth2)
        API->>ConnMgr: Initiate OAuth flow
        ConnMgr-->>SPA: Return OAuth consent URL
        SPA->>Admin: Redirect to Microsoft login
        Admin->>SPA: Consent granted (auth code)
        SPA->>API: POST /api/connectors/oauth/callback
        API->>ConnMgr: Exchange code for tokens
        ConnMgr->>KV: Store refresh token as secret
        ConnMgr->>ARM: Create Microsoft.Web/connections resource
        ARM-->>ConnMgr: API Connection resource ID
        ConnMgr->>ConnMgr: Save Connector (Status: Active, AzureApiConnectionId)
    else API Key connector (Slack, ACS, SendGrid)
        Admin->>SPA: Add Slack connector (paste bot token)
        SPA->>API: POST /api/connectors (type: Slack, auth: APIKey)
        API->>ConnMgr: Store credential
        ConnMgr->>KV: Store API key as secret
        ConnMgr->>ConnMgr: Save Connector (Status: Active)
    else Managed Identity connector (Azure services)
        Admin->>SPA: Add ACS connector (MSI)
        SPA->>API: POST /api/connectors (type: ACS, auth: ManagedIdentity)
        API->>ConnMgr: Configure MSI
        ConnMgr->>ARM: Create API Connection with MSI auth
        ARM-->>ConnMgr: API Connection resource ID
        ConnMgr->>ConnMgr: Save Connector (Status: Active)
    end

    Note over Admin,ARM: Logic App Generation (with connector resolution)

    Admin->>SPA: Design workflow & request Logic App export
    SPA->>API: POST /api/workflows/{id}/generate-logic-app
    API->>Gen: Generate Logic App definition

    Gen->>Gen: Map WorkflowSteps to Logic App actions
    Gen->>ConnMgr: Resolve connectors for each step
    ConnMgr-->>Gen: Connector configs + API Connection IDs

    Gen->>AI: Request AI-optimized connector mapping
    AI->>LLM: "Map these steps to Logic App connectors"
    LLM-->>AI: Connector recommendations
    AI-->>Gen: Optimized action mappings

    Gen->>Gen: Build ARM/Bicep template
    Note over Gen: Includes:<br/>- Microsoft.Web/connections resources<br/>- $connections parameters<br/>- triggers, actions, webhooks

    Gen->>Blob: Store generated template
    Gen-->>API: Return template URL & preview

    API-->>SPA: Logic App template (preview JSON)
    Admin->>SPA: Review & confirm deployment
    SPA->>API: POST /api/workflows/{id}/deploy-logic-app
    API->>ARM: Deploy ARM template (Logic App + API Connections)
    ARM-->>API: Deployment result (resource ID)
    API->>API: Update Workflow.LogicAppResourceId
    API-->>SPA: Deployment successful
```

---

## 8. Approval Timeout & Escalation Flow

```mermaid
stateDiagram-v2
    [*] --> Pending: Approval Request Created
    Pending --> Approved: User Approves
    Pending --> Rejected: User Rejects
    Pending --> Escalated: Timeout + OnTimeout=Escalate
    Pending --> AutoApproved: Timeout + OnTimeout=AutoApprove
    Pending --> AutoRejected: Timeout + OnTimeout=AutoReject

    Escalated --> Approved: Escalated User Approves
    Escalated --> Rejected: Escalated User Rejects

    Approved --> [*]: Step continues
    Rejected --> [*]: Step skipped/failed
    AutoApproved --> [*]: Step continues (auto)
    AutoRejected --> [*]: Step skipped/failed (auto)
```

---

## 9. Connector Lifecycle State Diagram

```mermaid
stateDiagram-v2
    [*] --> Created: POST /api/connectors

    Created --> Validating: System validates credentials
    Validating --> Active: Credentials valid
    Validating --> Failed: Credentials invalid

    state Active {
        [*] --> Ready
        Ready --> TokenRefresh: OAuth token near expiry
        TokenRefresh --> Ready: Refresh successful
        TokenRefresh --> Expired: Refresh failed
    }

    Active --> Failed: Runtime failure (revoked, network)
    Failed --> Validating: Admin re-validates / re-authenticates
    Expired --> Validating: Admin re-authenticates

    Active --> [*]: Admin deletes connector

    note right of Active
        Active connectors are used by:
        - Notification Service (send via channel)
        - Logic App Generator (embed API Connection)
    end note
```

### Connector Authentication Models

| Auth Model | Flow | Token Storage | API Connection |
|---|---|---|---|
| **OAuth 2.0** | Admin consents via browser → auth code → token exchange | Refresh token in Key Vault | `Microsoft.Web/connections` with `parameterValueType: Alternative` |
| **Service Principal** | App registration with client_id + client_secret | Client secret in Key Vault | `Microsoft.Web/connections` with service principal params |
| **API Key** | Admin pastes key/token directly | Key in Key Vault | N/A — uses HTTP action with `Authorization` header |
| **Connection String** | Admin pastes connection string | Connection string in Key Vault | N/A — uses SDK directly |
| **Managed Identity** | No secret needed — uses Azure MSI | No secret stored | `Microsoft.Web/connections` with MSI `identity` block |

---

## 10. Deployment Diagram

```mermaid
C4Deployment
    title Deployment Diagram - Azure Infrastructure

    Deployment_Node(azure, "Azure Cloud", "Microsoft Azure") {
        Deployment_Node(rg, "Resource Group", "workflow-ai-rg") {
            Deployment_Node(aks, "Azure Kubernetes Service", "AKS Cluster") {
                Container(apiPod, "Workflow API", ".NET 10 Container")
                Container(aiPod, "AI Agent Service", ".NET 10 Container")
                Container(approvalPod, "Approval Engine", ".NET 10 Container")
                Container(notifPod, "Notification Service", ".NET 10 Container")
                Container(genPod, "Logic App Generator", ".NET 10 Container")
            }
            Deployment_Node(appSvc, "Azure Static Web Apps", "Frontend Hosting") {
                Container(spaDeploy, "Frontend SPA", "React/Angular")
            }
            Deployment_Node(data, "Data Services", "") {
                ContainerDb(cosmosDeploy, "Cosmos DB", "Serverless/Provisioned")
                ContainerDb(sqlDeploy, "PostgreSQL", "Azure Database for PostgreSQL")
                Container(busDeploy, "Service Bus", "Premium tier")
                Container(blobDeploy, "Blob Storage", "Hot tier")
            }
            Deployment_Node(security, "Security", "") {
                Container(kvDeploy, "Key Vault", "Secrets, connection strings")
                Container(apimDeploy, "API Management", "Gateway, rate limiting")
            }
        }
    }

    Rel(spaDeploy, apimDeploy, "HTTPS")
    Rel(apimDeploy, apiPod, "HTTPS")
    Rel(apiPod, cosmosDeploy, "SDK")
    Rel(apiPod, sqlDeploy, "EF Core")
    Rel(apiPod, busDeploy, "AMQP")
    Rel(aiPod, busDeploy, "AMQP")
    Rel(approvalPod, busDeploy, "AMQP")
    Rel(notifPod, busDeploy, "AMQP")
```

---

## 11. Frontend Data Model (API DTOs)

These DTOs are what the frontend consumes via the REST API:

```mermaid
classDiagram
    class WorkflowDto {
        +Guid Id
        +String Name
        +String Description
        +String Status
        +WorkflowStepDto[] Steps
        +DateTime CreatedAt
        +String CreatedByName
    }

    class WorkflowStepDto {
        +Guid Id
        +Int OrderIndex
        +String Name
        +String StepType
        +Object Configuration
        +String RequiredRole
        +Int TimeoutMinutes
    }

    class WorkflowExecutionDto {
        +Guid Id
        +Guid WorkflowId
        +String WorkflowName
        +String Status
        +StepExecutionDto[] Steps
        +DateTime StartedAt
        +DateTime? CompletedAt
        +Double ProgressPercent
    }

    class StepExecutionDto {
        +Guid Id
        +String StepName
        +String StepType
        +String Status
        +Object OutputData
        +ApprovalRequestDto? Approval
        +DateTime? StartedAt
        +DateTime? CompletedAt
    }

    class ApprovalRequestDto {
        +Guid Id
        +String Title
        +String Description
        +Object ContextData
        +String Status
        +DateTime ExpiresAt
        +ApprovalActionDto[] Actions
    }

    class ApprovalActionDto {
        +String ActedByName
        +String Action
        +String Comment
        +String Channel
        +DateTime ActedAt
    }

    class ConnectorDto {
        +Guid Id
        +String Name
        +String ConnectorType
        +String AuthModel
        +String Status
        +String AzureApiConnectionId
        +Object Configuration
        +DateTime CreatedAt
        +DateTime? ExpiresAt
    }

    class ConnectorSummaryDto {
        +Guid Id
        +String Name
        +String ConnectorType
        +String Status
        +DateTime? ExpiresAt
    }

    class DashboardDto {
        +Int ActiveWorkflows
        +Int PendingApprovals
        +Int CompletedToday
        +RecentExecutionDto[] RecentExecutions
    }

    WorkflowDto "1" --> "*" WorkflowStepDto
    WorkflowStepDto "0..1" --> "0..1" ConnectorSummaryDto
    WorkflowExecutionDto "1" --> "*" StepExecutionDto
    StepExecutionDto "1" --> "0..1" ApprovalRequestDto
    ApprovalRequestDto "1" --> "*" ApprovalActionDto
```

---
