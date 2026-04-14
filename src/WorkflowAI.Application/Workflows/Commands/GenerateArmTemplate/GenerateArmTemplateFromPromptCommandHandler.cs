using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.Workflows.Commands.GenerateArmTemplate;

public sealed class GenerateArmTemplateFromPromptCommandHandler(
    ITenantConnectorRepository repository,
    IAnthropicService anthropicService,
    IBlobStorageService blobStorageService,
    IConfiguration configuration,
    IAzureDeploymentService? azureDeploymentService = null)
    : IRequestHandler<GenerateArmTemplateFromPromptCommand, Result<GenerateArmTemplateFromPromptResult>>
{
    // Phase 1: Pick relevant connectors from descriptions only (cheap, no API details)
    private const string ConnectorSelectionPromptTemplate = """
        You are a workflow designer. The user wants: "{userPrompt}"

        Available connectors:
        {connectorList}

        Select only the connectors needed to fulfil the user's request.

        Respond ONLY with valid JSON (no markdown, no extra text):
        {
          "connectors": ["ExactConnectorName1", "ExactConnectorName2"]
        }

        Rules:
        - Use the exact connector names as listed above.
        - Include only connectors that are relevant to the user's request.
        - If unsure, include all connectors.
        """;

    // Phase 2: Design the workflow using only the selected connectors' APIs
    private const string WorkflowPromptTemplate = """
        You are a workflow designer for an automation platform.
        The user wants to build a workflow: "{userPrompt}"

        Available connectors and their APIs:
        {connectorContext}

        Design a workflow that fulfils the user's request using only the available APIs listed above.

        Respond ONLY with valid JSON in exactly this format (no markdown, no extra text):
        {
          "workflowName": "short-kebab-case-name",
          "steps": [
            {
              "stepName": "UniqueActionName",
              "connectorName": "ExactConnectorName",
              "apiName": "exact/api-name"
            }
          ]
        }

        Rules:
        - workflowName must be lowercase kebab-case, max 40 characters.
        - stepName must be PascalCase and unique within the workflow.
        - connectorName and apiName must match exactly what is listed in the available APIs.
        - Only include steps for APIs that are available. Omit unavailable operations.
        - Order steps logically to fulfil the user's request.
        """;

    public async Task<Result<GenerateArmTemplateFromPromptResult>> Handle(
        GenerateArmTemplateFromPromptCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.From(request.TenantId);

        // Load active connectors for this tenant
        var connectors = await repository.GetByTenantAsync(tenantId, ct);
        var activeConnectors = connectors
            .Where(c => c.Status == TenantConnectorStatus.Active)
            .ToList();

        if (activeConnectors.Count == 0)
            return Error.NotFound(
                "Workflow.NoActiveConnectors",
                "No active connectors found for this tenant. Provision and activate a connector first.");

        // Load APIs per connector and build safe context (no credentials, no URLs)
        var connectorApis = new Dictionary<TenantConnector, IReadOnlyList<TenantConnectorApi>>();
        foreach (var connector in activeConnectors)
        {
            var apis = await repository.GetApisByConnectorAsync(connector.Id, ct);
            if (apis.Count > 0)
                connectorApis[connector] = apis;
        }

        if (connectorApis.Count == 0)
            return Error.NotFound(
                "Workflow.NoApisAvailable",
                "No API definitions found for the active connectors. Validate a connector first.");

        // Phase 1: Use Claude to select only relevant connectors (cheap: descriptions only, no API details)
        // Only run Phase 1 when there are enough connectors to justify the extra call
        if (connectorApis.Count > 1)
        {
            var selectedNames = await SelectConnectorsAsync(request.Prompt, connectorApis.Keys.ToList(), ct);
            if (selectedNames.Count > 0)
            {
                var selectedSet = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
                var filtered = connectorApis
                    .Where(kvp => selectedSet.Contains(kvp.Key.ConnectorType))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                // Only apply the filter when Phase 1 returned at least one valid connector
                if (filtered.Count > 0)
                    connectorApis = filtered;
            }
            // If Phase 1 fails or returns no valid connectors → fall back to all active connectors (connectorApis unchanged)
        }

        // Phase 2: Build the safe context with only selected connectors' APIs
        var safeContext = BuildSafeContext(connectorApis);

        var prompt = WorkflowPromptTemplate
            .Replace("{userPrompt}", request.Prompt)
            .Replace("{connectorContext}", safeContext);

        var aiResult = await anthropicService.CompleteAsync(prompt, cancellationToken: ct);
        if (!aiResult.Success)
            return Error.Unexpected(
                "Workflow.AiFailed",
                $"Claude failed to generate workflow: {aiResult.ErrorMessage}");

        var workflowPlan = ParseWorkflowPlan(aiResult.Content);
        if (workflowPlan is null)
            return Error.Unexpected(
                "Workflow.InvalidAiResponse",
                "Claude returned an unexpected response format.");

        // Build ARM template from the plan
        var builder = new TenantConnectorArmBuilder();
        var connectorsUsed = new List<string>();
        var missingApis = new List<string>();

        // Lookup index: connectorType → connector entity
        var connectorIndex = connectorApis.Keys
            .ToDictionary(c => c.ConnectorType, StringComparer.OrdinalIgnoreCase);

        // Lookup index: (connectorType, apiName) → api entity
        var apiIndex = connectorApis
            .SelectMany(kvp => kvp.Value.Select(api => (Connector: kvp.Key, Api: api)))
            .ToDictionary(
                x => (x.Connector.ConnectorType, x.Api.ApiName),
                x => (x.Connector, x.Api));

        var registeredPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Get API base URL from configuration (e.g., https://gokinfo.azurewebsites.net/api)
        var apiBaseUrl = configuration["WorkflowAI:ApiBaseUrl"]
            ?? "https://gokinfo.azurewebsites.net/api";

        foreach (var step in workflowPlan.Steps)
        {
            var key = (step.ConnectorName, step.ApiName);
            if (!apiIndex.TryGetValue(key, out var found))
            {
                missingApis.Add($"{step.ConnectorName}/{step.ApiName}");
                continue;
            }

            var (connector, api) = found;
            
            // Track used connectors
            if (!connectorsUsed.Contains(connector.ConnectorType))
                connectorsUsed.Add(connector.ConnectorType);

            // Build proxy endpoint URL: POST {apiBaseUrl}/apis/{apiId}/execute
            var proxyUrl = $"{apiBaseUrl}/apis/{api.Id.Value}/execute";
            
            // Add tenant and connector IDs as headers, plus Authorization for future JWT token
            var headers = new Dictionary<string, string>
            {
                ["X-Tenant-Id"] = tenantId.Value.ToString(),
                ["X-Connector-Id"] = connector.Id.Value.ToString(),
                ["Authorization"] = "234"
            };
            
            builder.AddStep(step.StepName, "POST", proxyUrl, headers);
        }

        if (connectorsUsed.Count == 0 && missingApis.Count > 0)
            return Error.Validation(
                "Workflow.AllStepsMissing",
                $"None of the requested APIs are available: {string.Join(", ", missingApis)}");

        var armJson = builder.Build(workflowPlan.WorkflowName);

        // Save files (no credentials needed in ARM template - proxy endpoint handles auth)
        var readyToDeployArmJson = await SaveOutputFilesAsync(workflowPlan, armJson, ct);

        // Deploy to Azure if requested (using ready-to-deploy ARM template)
        DeploymentInfo? deploymentInfo = null;
        if (request.Deploy && request.DeploymentConfig is not null && azureDeploymentService is not null)
        {
            deploymentInfo = await DeployToAzureAsync(
                request.DeploymentConfig,
                workflowPlan.WorkflowName,
                readyToDeployArmJson,
                ct);
        }

        return new GenerateArmTemplateFromPromptResult(
            workflowPlan.WorkflowName,
            armJson,
            connectorsUsed.Distinct().ToList(),
            missingApis,
            deploymentInfo);
    }

    // ─── private helpers ──────────────────────────────────────────────────────

    private async Task<List<string>> SelectConnectorsAsync(
        string userPrompt, IReadOnlyList<TenantConnector> connectors, CancellationToken ct)
    {
        try
        {
            var connectorList = string.Join("\n", connectors.Select(c =>
            {
                var desc = ParseConnectorDescription(c.Info);
                return $"- {c.ConnectorType}: {desc}";
            }));

            var prompt = ConnectorSelectionPromptTemplate
                .Replace("{userPrompt}", userPrompt)
                .Replace("{connectorList}", connectorList);

            var result = await anthropicService.CompleteAsync(prompt, cancellationToken: ct);
            if (!result.Success)
                return [];

            return ParseConnectorSelection(result.Content);
        }
        catch
        {
            return [];
        }
    }

    private static List<string> ParseConnectorSelection(string content)
    {
        try
        {
            var json = StripMarkdownFences(content.Trim());
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("connectors", out var arr))
                return [];

            return arr.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ParseConnectorDescription(string infoJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(infoJson);
            if (doc.RootElement.TryGetProperty("description", out var desc))
                return desc.GetString() ?? string.Empty;
        }
        catch (JsonException) { }
        return string.Empty;
    }

    private async Task<string> SaveOutputFilesAsync(
        WorkflowPlan plan, 
        string armJson, 
        CancellationToken ct)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var baseName = $"{plan.WorkflowName}-{timestamp}";
            var containerName = "workflow-outputs";

            var planJson = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });

            // Upload workflow plan
            var planBlobName = $"{baseName}-workflow-plan.json";
            var planUrl = await blobStorageService.UploadAsync(containerName, planBlobName, planJson, ct);
            
            // Upload ARM template
            var armBlobName = $"{baseName}-arm-template.json";
            var armUrl = await blobStorageService.UploadAsync(containerName, armBlobName, armJson, ct);
            
            // Upload empty deployment parameters file (proxy endpoint handles authentication)
            var emptyParameters = new
            {
                schema = "https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#",
                contentVersion = "1.0.0.0",
                parameters = new { }
            };
            var parametersJson = JsonSerializer.Serialize(emptyParameters, new JsonSerializerOptions { WriteIndented = true });
            var paramsBlobName = $"{baseName}-deployment-parameters.json";
            var paramsUrl = await blobStorageService.UploadAsync(containerName, paramsBlobName, parametersJson, ct);
            
            Console.WriteLine($"[WorkflowAI] Saved ARM template to local storage");
            
            return armJson;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorkflowAI] Warning: Failed to upload files to blob storage - {ex.Message}");
            // File upload is best-effort — never fail the request over it
            return armJson; // Return original if saving fails
        }
    }

    private static string InjectCredentialsIntoArmTemplate(string armJson, Dictionary<string, string> credentials)
    {
        var armTemplate = JsonSerializer.Deserialize<JsonDocument>(armJson);
        if (armTemplate == null) return armJson;

        var root = armTemplate.RootElement;
        var modifiedTemplate = new Dictionary<string, object>();

        // Copy schema and contentVersion
        if (root.TryGetProperty("$schema", out var schema))
            modifiedTemplate["$schema"] = schema.GetString()!;
        if (root.TryGetProperty("contentVersion", out var version))
            modifiedTemplate["contentVersion"] = version.GetString()!;

        // Modify parameters to include defaultValue
        if (root.TryGetProperty("parameters", out var parametersElement))
        {
            var modifiedParameters = new Dictionary<string, object>();
            foreach (var param in parametersElement.EnumerateObject())
            {
                var paramDef = new Dictionary<string, object>();
                
                // Copy type
                if (param.Value.TryGetProperty("type", out var typeElement))
                {
                    paramDef["type"] = typeElement.GetString()!;
                }

                // Add defaultValue if credential exists
                if (credentials.TryGetValue(param.Name, out var credentialValue))
                {
                    paramDef["defaultValue"] = credentialValue;
                }

                modifiedParameters[param.Name] = paramDef;
            }
            modifiedTemplate["parameters"] = modifiedParameters;
        }

        // Copy resources as-is
        if (root.TryGetProperty("resources", out var resourcesElement))
        {
            modifiedTemplate["resources"] = JsonSerializer.Deserialize<object>(resourcesElement.GetRawText())!;
        }

        return JsonSerializer.Serialize(modifiedTemplate, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string BuildDeploymentParametersJson(Dictionary<string, string> parameters)
    {
        var parametersObject = new Dictionary<string, object>();
        
        foreach (var kvp in parameters)
        {
            parametersObject[kvp.Key] = new { value = kvp.Value };
        }

        var deploymentParams = new
        {
            schema = "https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#",
            contentVersion = "1.0.0.0",
            parameters = parametersObject
        };

        return JsonSerializer.Serialize(deploymentParams, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private async Task<DeploymentInfo> DeployToAzureAsync(
        DeploymentConfig config,
        string workflowName,
        string readyToDeployArmJson,
        CancellationToken ct)
    {
        try
        {
            Console.WriteLine($"[WorkflowAI] Deploying workflow '{workflowName}' to Azure using ready-to-deploy ARM template...");

            // No need to pass parameters - credentials are already in ARM template as defaultValue
            var emptyParameters = new Dictionary<string, string>();

            var deploymentResult = await azureDeploymentService!.DeployArmTemplateAsync(
                config.SubscriptionId,
                config.ResourceGroupName,
                workflowName,
                readyToDeployArmJson,
                emptyParameters,
                ct);

            if (deploymentResult.Success)
            {
                Console.WriteLine($"[WorkflowAI] Deployment succeeded: {deploymentResult.LogicAppUrl}");
            }
            else
            {
                Console.WriteLine($"[WorkflowAI] Deployment failed: {deploymentResult.ErrorMessage}");
            }

            return new DeploymentInfo(
                deploymentResult.Status,
                deploymentResult.DeploymentId,
                deploymentResult.LogicAppResourceId,
                deploymentResult.LogicAppUrl,
                deploymentResult.Success ? DateTime.UtcNow : null,
                deploymentResult.ErrorMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorkflowAI] Error during deployment: {ex.Message}");
            return new DeploymentInfo(
                "Failed",
                null,
                null,
                null,
                null,
                ex.Message);
        }
    }

    /// <summary>
    /// Builds credentials dictionary from configuration based on connectors used.
    /// Maps connector-specific credentials from appsettings/local.settings.json.
    /// </summary>
    private Dictionary<string, string> BuildCredentialsFromConfiguration(List<string> connectorsUsed)
    {
        var credentials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var connectorType in connectorsUsed)
        {
            var prefix = SanitizeParamPrefix(connectorType);

            // Map standard credential fields from configuration
            // Format: Connectors__{ConnectorType}__{FieldName}
            var apiKey = configuration[$"Connectors:{connectorType}:ApiKey"];
            var instanceUrl = configuration[$"Connectors:{connectorType}:InstanceUrl"];
            var userLogin = configuration[$"Connectors:{connectorType}:UserLogin"];

            // Populate ARM parameter names with actual values
            if (!string.IsNullOrEmpty(apiKey))
                credentials[$"{prefix}_api_key"] = apiKey;

            if (!string.IsNullOrEmpty(instanceUrl))
                credentials[$"{prefix}_instance_url"] = instanceUrl;

            if (!string.IsNullOrEmpty(userLogin))
                credentials[$"{prefix}_user_login"] = userLogin;
        }

        return credentials;
    }

    private static string BuildSafeContext(Dictionary<TenantConnector, IReadOnlyList<TenantConnectorApi>> connectorApis)
    {
        var context = connectorApis.Select(kvp => new
        {
            connectorName = kvp.Key.ConnectorType,
            apis = kvp.Value.Select(a => new
            {
                apiName = a.ApiName,
                method = a.HttpMethod
            })
        });

        return JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
    }

    private static WorkflowPlan? ParseWorkflowPlan(string content)
    {
        try
        {
            var json = StripMarkdownFences(content.Trim());
            return JsonSerializer.Deserialize<WorkflowPlan>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ConnectorMetadata ParseConnectorMetadata(string metadataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            var root = doc.RootElement;

            var authType = root.TryGetProperty("authType", out var auth)
                ? auth.GetString() ?? "APIKey"
                : "APIKey";

            var requiredFields = new List<string>();
            if (root.TryGetProperty("requiredFields", out var fields))
            {
                foreach (var field in fields.EnumerateArray())
                {
                    var s = field.GetString();
                    if (s is not null)
                        requiredFields.Add(s);
                }
            }

            var baseUrl = root.TryGetProperty("baseUrl", out var url)
                ? url.GetString()
                : null;

            return new ConnectorMetadata(authType, requiredFields, baseUrl);
        }
        catch (JsonException)
        {
            return new ConnectorMetadata("APIKey", [], null);
        }
    }

    private static void RegisterConnectorParameters(
        TenantConnectorArmBuilder builder,
        ConnectorMetadata metadata,
        string paramPrefix,
        List<string> connectorsUsed,
        string connectorName,
        HashSet<string> registeredPrefixes)
    {
        if (!registeredPrefixes.Add(paramPrefix))
            return;

        connectorsUsed.Add(connectorName);

        foreach (var field in metadata.RequiredFields)
        {
            var paramName = $"{paramPrefix}_{field}".ToLowerInvariant().Replace("-", "_");
            builder.AddParameter(paramName, secure: true);
        }
    }

    private static string SanitizeParamPrefix(string connectorName) =>
        connectorName.ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");

    private static string StripMarkdownFences(string content)
    {
        if (content.StartsWith("```"))
        {
            var firstNewline = content.IndexOf('\n');
            var lastFence = content.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                return content[(firstNewline + 1)..lastFence].Trim();
        }
        return content;
    }

    // ─── nested types ─────────────────────────────────────────────────────────

    private sealed record WorkflowPlan(
        string WorkflowName,
        IReadOnlyList<WorkflowStep> Steps);

    private sealed record WorkflowStep(
        string StepName,
        string ConnectorName,
        string ApiName);

    private sealed record ConnectorMetadata(
        string AuthType,
        IReadOnlyList<string> RequiredFields,
        string? BaseUrl);
}
