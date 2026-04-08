using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateMcpWorkflows;

public sealed class GenerateMcpWorkflowsCommandHandler(
    ITenantConnectorRepository repository,
    IAnthropicService anthropicService,
    ILogger<GenerateMcpWorkflowsCommandHandler> logger)
    : IRequestHandler<GenerateMcpWorkflowsCommand, Result<GenerateMcpWorkflowsResult>>
{
    // Tool definitions passed to Claude — Claude calls these to produce structured output.
    // Each tool call Claude makes becomes one route/template entry in the result.
    private static readonly IReadOnlyList<AIToolDefinition> WorkflowGenerationTools =
    [
        new AIToolDefinition(
            Name: "create_api_route",
            Description: "Register an API route that exposes a connector operation as a callable workflow step.",
            ParametersJson: """
                {
                  "type": "object",
                  "properties": {
                    "routeName":    { "type": "string", "description": "Unique route identifier, e.g. connector-name/operation" },
                    "method":       { "type": "string", "enum": ["GET","POST","PUT","PATCH","DELETE"] },
                    "path":         { "type": "string", "description": "URL path, e.g. /connectors/{connectorName}/contacts/search" },
                    "description":  { "type": "string", "description": "What this route does" },
                    "inputSchema":  { "type": "object", "description": "JSON Schema of the request body" },
                    "outputSchema": { "type": "object", "description": "JSON Schema of the response" }
                  },
                  "required": ["routeName","method","path","description"]
                }
                """),

        new AIToolDefinition(
            Name: "create_workflow_template",
            Description: "Define a reusable workflow template that chains one or more connector operations together.",
            ParametersJson: """
                {
                  "type": "object",
                  "properties": {
                    "templateName": { "type": "string", "description": "Human-readable template name, e.g. 'Sync New Contacts'" },
                    "description":  { "type": "string", "description": "What this workflow automates" },
                    "trigger":      { "type": "string", "description": "What starts the workflow, e.g. 'webhook', 'schedule', 'manual'" },
                    "steps": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "stepName":   { "type": "string" },
                          "routeName":  { "type": "string", "description": "References a create_api_route routeName" },
                          "inputMapping": { "type": "object", "description": "Maps trigger/prior step output to this step's input" }
                        },
                        "required": ["stepName","routeName"]
                      }
                    }
                  },
                  "required": ["templateName","description","trigger","steps"]
                }
                """)
    ];

    private const string WorkflowGenerationPromptTemplate = """
        You are a workflow automation architect.

        The connector "{connectorName}" is active and provides the following API operations:
        {apiOperations}

        Using the tools provided:
        1. Call `create_api_route` for each meaningful API operation to expose it as a workflow step.
           Focus on the most useful operations (up to 10 routes).

        2. Call `create_workflow_template` to define 2-3 practical automation workflows that
           combine these routes to solve common use-cases for "{connectorName}" users.

        Generate routes and templates that are practical and immediately usable.
        """;

    public async Task<Result<GenerateMcpWorkflowsResult>> Handle(
        GenerateMcpWorkflowsCommand request, CancellationToken ct)
    {
        var id = TenantConnectorId.From(request.TenantConnectorId);

        var connector = await repository.GetByIdAsync(id, ct);
        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound", "Connector not found.");

        if (connector.Status != TenantConnectorStatus.Active)
            return Error.Validation(
                "TenantConnector.NotActive",
                $"Connector must be Active to generate workflows. Current status: {connector.Status.Name}");

        var apis = await repository.GetApisByConnectorAsync(id, ct);

        var apiSummary = apis.Count > 0
            ? string.Join("\n", apis.Select(a => $"- {a.ApiName}: {a.HttpMethod} {a.UrlTemplate}"))
            : "(No API operations discovered yet — generate generic workflow templates based on the connector type.)";

        var prompt = WorkflowGenerationPromptTemplate
            .Replace("{connectorName}", connector.ConnectorType)
            .Replace("{apiOperations}", apiSummary);

        var aiResult = await anthropicService.CompleteWithToolsAsync(
            prompt, WorkflowGenerationTools, cancellationToken: ct);

        if (!aiResult.Success)
        {
            logger.LogWarning("Claude workflow generation failed for {Connector}: {Error}",
                connector.ConnectorType, aiResult.ErrorMessage);
            return Error.Unexpected(
                "TenantConnector.GenerationFailed",
                $"Claude failed to generate workflows: {aiResult.ErrorMessage}");
        }

        var toolCalls = aiResult.ToolCalls ?? [];
        var apiRoutes    = toolCalls.Where(t => t.ToolName == "create_api_route").Select(t => t.InputJson).ToList();
        var workflowDefs = toolCalls.Where(t => t.ToolName == "create_workflow_template").Select(t => t.InputJson).ToList();

        var apiRoutesJson    = JsonSerializer.Serialize(apiRoutes);
        var workflowDefsJson = JsonSerializer.Serialize(workflowDefs);

        return new GenerateMcpWorkflowsResult(
            ConnectorType: connector.ConnectorType,
            ApiRoutesCount: apiRoutes.Count,
            WorkflowTemplatesCount: workflowDefs.Count,
            ApiRoutes: apiRoutesJson,
            WorkflowDefs: workflowDefsJson);
    }

}
