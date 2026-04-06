using System.Text.Json;
using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Templates;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.GenerateMcpWorkflows;

public sealed class GenerateMcpWorkflowsCommandHandler(
    ITenantConnectorRepository repository,
    ITemplateRepository templateRepository,
    IClaudeAIService claudeAIService)
    : IRequestHandler<GenerateMcpWorkflowsCommand, Result<GenerateMcpWorkflowsResult>>
{
    public async Task<Result<GenerateMcpWorkflowsResult>> Handle(
        GenerateMcpWorkflowsCommand request, CancellationToken cancellationToken)
    {
        var id = TenantConnectorId.From(request.TenantConnectorId);
        var connector = await repository.GetByIdAsync(id, cancellationToken);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound",
                $"TenantConnector {request.TenantConnectorId} not found.");

        if (connector.Status != TenantConnectorStatus.Active)
            return Error.Validation("TenantConnector.NotActive",
                $"Connector must be Active before generating workflows. Current status: {connector.Status.Name}");

        var tools = new[]
        {
            new AIToolDefinition(
                "create_api_route",
                "Define a new API route for the connector",
                """{"type":"object","properties":{"method":{"type":"string"},"path":{"type":"string"},"description":{"type":"string"},"requestBody":{"type":"object"},"responseSchema":{"type":"object"}},"required":["method","path","description"]}"""),
            new AIToolDefinition(
                "create_workflow_template",
                "Define a workflow template for the connector",
                """{"type":"object","properties":{"name":{"type":"string"},"trigger":{"type":"string"},"steps":{"type":"array","items":{"type":"object"}},"description":{"type":"string"}},"required":["name","trigger","steps","description"]}""")
        };

        var prompt = $"""
            You are an API and workflow generator for a workflow automation platform.
            Based on the following connector metadata and info for "{connector.ConnectorName}",
            generate useful API routes and workflow templates.

            METADATA:
            {connector.Metadata}

            INFO:
            {connector.Info}

            Use the create_api_route tool to define each API route.
            Use the create_workflow_template tool to define each workflow template.
            Generate at least 3 API routes and 2 workflow templates that would be useful for this connector.
            """;

        var aiResult = await claudeAIService.CompleteWithToolsAsync(prompt, tools, cancellationToken);

        if (!aiResult.Success)
            return Error.Unexpected("TenantConnector.WorkflowGenerationFailed",
                aiResult.ErrorMessage ?? "Claude failed to generate workflows.");

        var toolCalls = aiResult.ToolCalls ?? [];

        var apiRoutes = toolCalls
            .Where(t => t.ToolName == "create_api_route")
            .Select(t => t.InputJson)
            .ToList();

        var workflowTemplates = toolCalls
            .Where(t => t.ToolName == "create_workflow_template")
            .Select(t => t.InputJson)
            .ToList();

        // Persist each API route as a WorkflowTemplate (category = connector name, type = "ApiRoute")
        foreach (var routeJson in apiRoutes)
        {
            var routeDoc = JsonDocument.Parse(routeJson).RootElement;
            var method = routeDoc.TryGetProperty("method", out var m) ? m.GetString() : "GET";
            var path = routeDoc.TryGetProperty("path", out var p) ? p.GetString() : string.Empty;
            var description = routeDoc.TryGetProperty("description", out var d) ? d.GetString() : null;

            var defaultSteps = JsonSerializer.Serialize(new[]
            {
                new
                {
                    name = $"{method} {path}",
                    stepType = "Action",
                    httpMethod = method,
                    configuration = routeJson
                }
            });

            var template = WorkflowTemplate.Create(
                name: $"{connector.ConnectorName}: {method} {path}",
                description: description,
                category: $"{connector.ConnectorName}/ApiRoute",
                defaultSteps: defaultSteps);

            await templateRepository.AddAsync(template, cancellationToken);
        }

        // Persist each workflow template as a WorkflowTemplate (category = connector name, type = "Workflow")
        foreach (var wfJson in workflowTemplates)
        {
            var wfDoc = JsonDocument.Parse(wfJson).RootElement;
            var name = wfDoc.TryGetProperty("name", out var n) ? n.GetString() : "Unnamed Workflow";
            var description = wfDoc.TryGetProperty("description", out var d) ? d.GetString() : null;
            var stepsElement = wfDoc.TryGetProperty("steps", out var s) ? s.GetRawText() : "[]";

            var template = WorkflowTemplate.Create(
                name: $"{connector.ConnectorName}: {name}",
                description: description,
                category: $"{connector.ConnectorName}/Workflow",
                defaultSteps: stepsElement);

            await templateRepository.AddAsync(template, cancellationToken);
        }

        return new GenerateMcpWorkflowsResult(
            JsonSerializer.Serialize(apiRoutes),
            JsonSerializer.Serialize(workflowTemplates));
    }
}
