# Workflow-AI — Connector Management & Logic App Generation Guide

> Focused guide for configuring, managing connectors and generating Azure Logic App ARM templates.

---

## Table of Contents

1. [Azure Setup for Connectors & Logic Apps](#1-azure-setup-for-connectors--logic-apps)
2. [Application Settings](#2-application-settings)
3. [Connector Management API](#3-connector-management-api)
4. [Logic App Generation API](#4-logic-app-generation-api)
5. [End-to-End Walkthrough](#5-end-to-end-walkthrough)

---

## 1. Azure Setup for Connectors & Logic Apps

Only these Azure resources are required for connector management and Logic App generation:

### 1.1 Azure Key Vault (stores connector secrets)

```bash
az keyvault create \
  --name workflow-ai-kv \
  --resource-group workflow-ai-rg \
  --enable-rbac-authorization true

# Grant your app identity "Key Vault Secrets Officer" role
az role assignment create \
  --role "Key Vault Secrets Officer" \
  --assignee <app-principal-id-or-your-user-object-id> \
  --scope /subscriptions/<sub-id>/resourceGroups/workflow-ai-rg/providers/Microsoft.KeyVault/vaults/workflow-ai-kv
```

### 1.2 Azure SQL Database (stores connector metadata)

The `Connectors` and `ConnectorCredentials` tables live alongside other app tables. Apply the EF Core migration:

```bash
cd src/WorkflowAI.Infrastructure

dotnet ef migrations add AddConnectors \
  --startup-project ../WorkflowAI.Functions \
  --context WorkflowAIDbContext

dotnet ef database update \
  --startup-project ../WorkflowAI.Functions \
  --context WorkflowAIDbContext
```

**Tables created:**

| Table | Key Columns | Purpose |
|---|---|---|
| `Connectors` | Id, Name, ConnectorType, AuthModel, Status, AzureApiConnectionId, Configuration | Connector definition and lifecycle state |
| `ConnectorCredentials` | Id, ConnectorId, KeyVaultSecretName, KeyVaultSecretVersion, CredentialType, ExpiresAt | Key Vault secret reference (never stores raw secrets) |

### 1.3 Microsoft Entra ID App Registration (for OAuth connectors)

Required only if you use OAuth2 connectors (Office 365, Teams rich integration):

```bash
# Create app registration
az ad app create \
  --display-name "Workflow-AI Connectors" \
  --web-redirect-uris "https://<your-function-app>.azurewebsites.net/api/connectors/oauth/callback" \
  --required-resource-accesses '[{
    "resourceAppId": "00000003-0000-0000-c000-000000000000",
    "resourceAccess": [
      {"id": "e383f46e-2787-4529-855e-0e479a3ffac0", "type": "Scope"},
      {"id": "024d486e-b451-40bb-833d-3e66d98c5c73", "type": "Scope"}
    ]
  }]'

# Note the appId and create a client secret
az ad app credential reset --id <app-id> --display-name "connector-secret"
```

| Permission | Scope | Purpose |
|---|---|---|
| `Mail.Send` | Delegated | Send emails via Office 365 connector |
| `ChannelMessage.Send` | Delegated | Post to Teams channels |

Store the `clientId` and `clientSecret` in your app settings (see section 2).

### 1.4 Azure Communication Services (for email connectors)

```bash
az communication create \
  --name workflow-ai-acs \
  --resource-group workflow-ai-rg \
  --data-location unitedstates

# Get connection string
az communication list-key --name workflow-ai-acs --resource-group workflow-ai-rg
```

Configure an email domain in the Azure Portal: ACS resource > Email > Domains > Add Azure-managed domain.

---

## 2. Application Settings

### 2.1 Required Settings for Connector Management

Add these to `local.settings.json` (local) or Azure Function App Settings (production):

```json
{
  "Values": {
    "KeyVault__Uri": "https://workflow-ai-kv.vault.azure.net/",

    "OAuth__ClientId": "<entra-app-client-id>",
    "OAuth__ClientSecret": "<entra-app-client-secret>",
    "OAuth__RedirectUri": "https://<func-app>.azurewebsites.net/api/connectors/oauth/callback",

    "Teams__WebhookSecret": "<teams-webhook-secret>"
  },
  "ConnectionStrings": {
    "SqlDb": "Server=...;Database=workflow-ai-db;...",
    "AzureCommunicationServices": "endpoint=https://...;accesskey=..."
  }
}
```

### 2.2 Settings Reference

| Key | Required For | Description |
|---|---|---|
| `KeyVault:Uri` | All connectors | Key Vault URI — all secrets stored here |
| `ConnectionStrings:SqlDb` | All connectors | SQL DB for connector metadata |
| `OAuth:ClientId` | OAuth2 connectors | Entra ID app registration client ID |
| `OAuth:ClientSecret` | OAuth2 connectors | Entra ID app registration client secret |
| `OAuth:RedirectUri` | OAuth2 connectors | OAuth callback URL (must match Entra app registration) |
| `Teams:WebhookSecret` | Teams webhooks | Verifies incoming Teams webhook requests |
| `ConnectionStrings:AzureCommunicationServices` | ACS email connector | ACS connection string |

---

## 3. Connector Management API

Base URL: `http://localhost:7071/api` (local) or `https://<func-app>.azurewebsites.net/api`

### Connector Types & Auth Models

| Connector Type | Supported Auth Models | Needs API Connection? | Use Case |
|---|---|---|---|
| `Office365` | `OAuth2`, `ServicePrincipal` | Yes | Send emails via Outlook |
| `Teams` | `OAuth2`, `ServicePrincipal` | Yes | Adaptive cards, channel posts |
| `ACS` | `ConnectionString`, `ManagedIdentity` | No | Transactional email |
| `SendGrid` | `APIKey` | No | Bulk email |
| `Custom` | `APIKey`, `ConnectionString` | No | Any REST API |

### Connector Lifecycle

```
Created → Validating → Active → Expired
                ↓                   ↓
              Failed ← ← ← ← ← ← ←
```

---

### 3.1 Create Connector (API Key / Connection String)

For connectors that use static credentials (ACS, SendGrid, Custom).

```
POST /connectors
Content-Type: application/json
```

**Request:**

```json
{
  "name": "SendGrid Production",
  "connectorType": "SendGrid",
  "authModel": "APIKey",
  "configuration": "{\"fromAddress\": \"noreply@company.com\"}",
  "secret": "SG.xxxxxxxxxxxxxxxxxxxx"
}
```

**Response (201):**

```json
{
  "connectorId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "oAuthConsentUrl": null
}
```

**What happens:**
1. Connector record created in SQL (status: `Created`)
2. Secret stored in Key Vault as `connector-{id}-secret`
3. ConnectorCredential record created (points to Key Vault secret name)
4. Connector status set to `Active`

**ACS Connection String example:**

```json
{
  "name": "ACS Email Production",
  "connectorType": "ACS",
  "authModel": "ConnectionString",
  "configuration": "{\"senderAddress\": \"noreply@contoso.com\"}",
  "secret": "endpoint=https://workflow-ai-acs.unitedstates.communication.azure.com/;accesskey=abc123..."
}
```

---

### 3.2 Create Connector (OAuth2)

For connectors that require user consent (Office 365, Teams rich).

```
POST /connectors
Content-Type: application/json
```

**Request:**

```json
{
  "name": "Office 365 Production",
  "connectorType": "Office365",
  "authModel": "OAuth2",
  "managedApiId": "office365"
}
```

**Response (201):**

```json
{
  "connectorId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "oAuthConsentUrl": "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id=...&response_type=code&redirect_uri=...&scope=Mail.Send&state=b2c3d4e5-f6a7-8901-bcde-f12345678901"
}
```

**Next step:** Redirect the user to `oAuthConsentUrl`. After consent, Microsoft redirects back to your callback URL with `code` and `state` parameters.

---

### 3.3 Complete OAuth Flow

Called after the user consents and is redirected back with an authorization code.

```
POST /connectors/oauth/callback
Content-Type: application/json
```

**Request:**

```json
{
  "code": "0.AXkAbC1dEf2gHi3jKl4mNo5pQr...",
  "state": "b2c3d4e5-f6a7-8901-bcde-f12345678901"
}
```

| Field | Description |
|---|---|
| `code` | Authorization code from the OAuth provider redirect |
| `state` | The connector ID — returned in the create step |

**Response (204 No Content)** on success.

**What happens:**
1. Exchanges auth code for access token + refresh token via Microsoft token endpoint
2. Stores refresh token in Key Vault as `connector-{id}-refresh-token`
3. Creates ConnectorCredential record
4. Provisions Azure `Microsoft.Web/connections` resource (for Logic App integration)
5. Updates connector: status `Active`, sets `AzureApiConnectionId` and `ExpiresAt`

**Sequence diagram:**

```
Frontend                    API                     Entra ID          Key Vault        Azure RM
   |                         |                         |                 |                |
   |-- POST /connectors ---->|                         |                 |                |
   |<- {connectorId, url} ---|                         |                 |                |
   |                         |                         |                 |                |
   |-- Redirect user to url ------------------------------------------>|                |
   |<- User consents, redirect back with code ---------|                 |                |
   |                         |                         |                 |                |
   |-- POST /oauth/callback->|                         |                 |                |
   |                         |-- Exchange code ------->|                 |                |
   |                         |<- tokens + expiry ------|                 |                |
   |                         |-- Store refresh token ----------------->|                |
   |                         |-- Provision API Connection -------------------------------->|
   |                         |<- resource ID -------------------------------------------------|
   |<-------- 204 -----------|                         |                 |                |
```

---

### 3.4 List Connectors

```
GET /connectors
```

**Response (200):**

```json
[
  {
    "id": "a1b2c3d4-...",
    "name": "SendGrid Production",
    "connectorType": "SendGrid",
    "authModel": "APIKey",
    "status": "Active",
    "azureApiConnectionId": null,
    "configuration": "{\"fromAddress\": \"noreply@company.com\"}",
    "createdAt": "2026-03-24T10:00:00Z",
    "expiresAt": null
  },
  {
    "id": "b2c3d4e5-...",
    "name": "Office 365 Production",
    "connectorType": "Office365",
    "authModel": "OAuth2",
    "status": "Active",
    "azureApiConnectionId": "/subscriptions/.../providers/Microsoft.Web/connections/office365-b2c3d4e5",
    "configuration": null,
    "createdAt": "2026-03-24T11:00:00Z",
    "expiresAt": "2026-04-24T11:00:00Z"
  }
]
```

---

### 3.5 Get Connector

```
GET /connectors/{connectorId}
```

**Response (200):** Single connector object (same shape as list items).

**Response (404):**

```json
{ "code": "Connector.NotFound", "message": "Connector not found." }
```

---

### 3.6 Validate Connector

Tests whether credentials are still valid by calling the target service.

```
POST /connectors/{connectorId}/validate
```

**Response (200):**

```json
true
```

**What happens:**
1. Sets status to `Validating`
2. Calls target service (Microsoft Graph `/me`, ACS health check, etc.)
3. On success: status back to `Active`
4. On failure: status set to `Failed`

Use this to check connectors before generating Logic Apps or after receiving delivery failures.

---

### 3.7 Delete Connector

Removes the connector and cleans up all associated resources.

```
DELETE /connectors/{connectorId}
```

**Response (204 No Content)**

**Cleanup performed:**
1. Key Vault secret deleted
2. Azure API Connection resource deprovisioned (if exists)
3. ConnectorCredential record removed
4. Connector record removed

---

### 3.8 Token Refresh (Automatic)

OAuth2 connector tokens are refreshed automatically by a timer trigger that runs every 5 minutes. It checks for connectors expiring within 15 minutes and refreshes their tokens.

No API call needed — this is fully automatic. If a refresh fails, the connector status is set to `Expired` and an admin must re-authenticate.

---

## 4. Logic App Generation API

### 4.1 How Logic App Generation Works

The generator takes a Workflow definition and produces a deployable ARM template. Each workflow step is mapped to a Logic App action:

| Step Type | Logic App Action | Connector Needed? |
|---|---|---|
| `HumanApproval` | `HttpWebhook` action (pauses until callback) | No |
| `Notification` | `ApiConnection` action (uses connector) | Yes — for email/Teams |
| `Action` | `Http` action | Optional |

When a step has a `ConnectorId`, the generator:
1. Loads the connector from the database
2. Adds a `Microsoft.Web/connections` resource to the ARM template
3. Injects `$connections` parameters into the Logic App definition
4. Maps the step to an `ApiConnection` action referencing that connection

### 4.2 Generate ARM Template

> **Note:** The generation logic is fully implemented in `LogicAppScriptGenerator`. The HTTP trigger endpoint needs to be wired. You can invoke it programmatically or add the HTTP trigger function.

**Programmatic usage (in a handler or service):**

```csharp
var workflow = await workflowRepository.GetByIdAsync(workflowId, ct);
var result = await logicAppScriptGenerator.GenerateArmTemplateAsync(workflow, ct);

// result.Content  → ARM JSON string
// result.FileName → e.g., "Invoice-Approval-logic-app.json"
// result.Success  → true/false
```

**Planned HTTP endpoint:**

```
POST /workflows/{workflowId}/generate-logic-app
```

**Response (200):**

```json
{
  "content": "<ARM template JSON>",
  "fileName": "Invoice-Approval-logic-app.json",
  "success": true,
  "errorMessage": null
}
```

### 4.3 Generated ARM Template Structure

For a workflow with 2 steps (Approval → Email via Office 365 connector):

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": [
    {
      "type": "Microsoft.Web/connections",
      "apiVersion": "2016-06-01",
      "name": "office365-connection",
      "location": "[resourceGroup().location]",
      "properties": {
        "api": {
          "id": "[subscriptionResourceId('Microsoft.Web/locations/managedApis', resourceGroup().location, 'office365')]"
        }
      }
    },
    {
      "type": "Microsoft.Logic/workflows",
      "apiVersion": "2019-05-01",
      "name": "Invoice-Approval",
      "location": "[resourceGroup().location]",
      "properties": {
        "parameters": {
          "$connections": {
            "value": {
              "office365": {
                "connectionId": "[resourceId('Microsoft.Web/connections', 'office365-connection')]",
                "connectionName": "office365-connection",
                "id": "[subscriptionResourceId('Microsoft.Web/locations/managedApis', resourceGroup().location, 'office365')]"
              }
            }
          }
        },
        "definition": {
          "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
          "contentVersion": "1.0.0.0",
          "triggers": {
            "manual": { "type": "Request", "kind": "Http" }
          },
          "actions": {
            "Manager_Approval": {
              "type": "HttpWebhook",
              "inputs": { "method": "POST", "uri": "https://your-func/api/approvals/subscribe" }
            },
            "Send_Email": {
              "type": "ApiConnection",
              "inputs": { "method": "POST", "uri": "/v2/Mail" }
            }
          }
        }
      }
    }
  ]
}
```

### 4.4 Deploy the Generated Template

```bash
# Save the generated content to a file, then deploy
az deployment group create \
  --resource-group workflow-ai-rg \
  --template-file Invoice-Approval-logic-app.json
```

### 4.5 Logic App + Connector Relationship

```
                    ┌─────────────────────────────────────────┐
                    │          Generated ARM Template          │
                    ├─────────────────────────────────────────┤
                    │                                         │
                    │  Microsoft.Web/connections               │
                    │  ├─ office365-connection ← ─ ─ ─ ─ ┐   │
                    │                                     │   │
                    │  Microsoft.Logic/workflows           │   │
                    │  ├─ triggers: HTTP Request           │   │
                    │  ├─ actions:                         │   │
                    │  │   ├─ Approval (HttpWebhook)       │   │
                    │  │   └─ Send_Email (ApiConnection) ──┘   │
                    │  └─ $connections: office365 ref          │
                    │                                         │
                    └─────────────────────────────────────────┘
                                       │
                     Generated from    │
                                       ▼
┌──────────────────┐    ┌────────────────────┐    ┌──────────────────────┐
│    Workflow       │───▶│   WorkflowStep     │───▶│     Connector        │
│  (Cosmos DB)      │    │  - Approval        │    │  - Office365         │
│                   │    │  - Send Email      │    │  - OAuth2            │
│                   │    │    ConnectorId: FK──│───▶│  - Status: Active    │
│                   │    │                    │    │  - ApiConnectionId   │
└──────────────────┘    └────────────────────┘    └──────────┬───────────┘
                                                             │
                                                             ▼
                                                  ┌─────────────────────┐
                                                  │ ConnectorCredential  │
                                                  │ KeyVaultSecretName:  │
                                                  │  connector-{id}-     │
                                                  │  refresh-token       │
                                                  └─────────┬───────────┘
                                                            │
                                                            ▼
                                                  ┌─────────────────────┐
                                                  │   Azure Key Vault    │
                                                  │  (actual secret)     │
                                                  └─────────────────────┘
```

---

## 5. End-to-End Walkthrough

Complete example: set up connectors, create a workflow, generate a Logic App.

### Step 1 — Create an API Key connector (SendGrid)

```bash
curl -X POST http://localhost:7071/api/connectors \
  -H "Content-Type: application/json" \
  -d '{
    "name": "SendGrid Production",
    "connectorType": "SendGrid",
    "authModel": "APIKey",
    "configuration": "{\"fromAddress\": \"noreply@company.com\"}",
    "secret": "SG.xxxxxxxxxxxxxxxxxxxx"
  }'
```

```json
← 201: { "connectorId": "aaaa-...", "oAuthConsentUrl": null }
```

### Step 2 — Create an OAuth connector (Office 365)

```bash
curl -X POST http://localhost:7071/api/connectors \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Office 365 Email",
    "connectorType": "Office365",
    "authModel": "OAuth2",
    "managedApiId": "office365"
  }'
```

```json
← 201: {
  "connectorId": "bbbb-...",
  "oAuthConsentUrl": "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?..."
}
```

### Step 3 — Complete OAuth consent

Open the `oAuthConsentUrl` in a browser. After user consents:

```bash
curl -X POST http://localhost:7071/api/connectors/oauth/callback \
  -H "Content-Type: application/json" \
  -d '{ "code": "0.AXkA...", "state": "bbbb-..." }'
```

```
← 204 No Content
```

### Step 4 — Verify both connectors are Active

```bash
curl http://localhost:7071/api/connectors
```

```json
← 200: [
  { "name": "SendGrid Production", "status": "Active", "azureApiConnectionId": null },
  { "name": "Office 365 Email", "status": "Active", "azureApiConnectionId": "/subscriptions/.../Microsoft.Web/connections/office365-bbbb" }
]
```

### Step 5 — Create a workflow with steps linked to connectors

```bash
curl -X POST http://localhost:7071/api/workflows \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Purchase Order Review",
    "description": "Manager approves PO, result emailed via O365",
    "steps": [
      {
        "name": "Manager Approval",
        "stepType": "HumanApproval",
        "requiredRole": "Admin",
        "timeoutMinutes": 1440,
        "onTimeoutAction": "Escalate"
      },
      {
        "name": "Send Approval Email",
        "stepType": "Notification",
        "configuration": "{\"to\": \"procurement@company.com\"}",
        "timeoutMinutes": 5
      }
    ]
  }'
```

```json
← 201: { "value": "cccc-..." }
```

> **Note:** To link steps to connectors, the `WorkflowStep.ConnectorId` property needs to be set. This can be done via the Update Workflow API or by extending the create step DTO to accept a `connectorId` field.

### Step 6 — Generate Logic App ARM template

```bash
# Programmatically or via the planned endpoint:
curl -X POST http://localhost:7071/api/workflows/cccc-.../generate-logic-app
```

The generator:
1. Reads all workflow steps
2. Finds the Notification step → loads its Connector (Office 365)
3. Adds `Microsoft.Web/connections/office365-connection` to ARM resources
4. Maps Approval → `HttpWebhook`, Notification → `ApiConnection`
5. Returns deployable ARM JSON

### Step 7 — Deploy to Azure

```bash
# Save the response content to a file
az deployment group create \
  --resource-group workflow-ai-rg \
  --template-file Purchase-Order-Review-logic-app.json
```

### Step 8 — Validate connector health (ongoing)

```bash
curl -X POST http://localhost:7071/api/connectors/bbbb-.../validate
```

```json
← 200: true
```

Token refresh for OAuth connectors happens automatically every 5 minutes.

---

## Error Responses

All APIs return consistent error responses:

| HTTP Status | Error Type | Example |
|---|---|---|
| 400 | Validation | `{ "code": "Connector.InvalidType", "message": "Invalid connector type: Unknown" }` |
| 404 | Not Found | `{ "code": "Connector.NotFound", "message": "Connector not found." }` |
| 403 | Unauthorized | `{ "code": "Auth.Required", "message": "User must be authenticated." }` |
| 500 | Failure | `{ "code": "Internal", "message": "..." }` |
