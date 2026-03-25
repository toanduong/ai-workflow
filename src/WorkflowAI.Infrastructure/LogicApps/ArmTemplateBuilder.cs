using System.Text.Json;
using System.Text.Json.Nodes;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Infrastructure.LogicApps;

public sealed class ArmTemplateBuilder
{
    private readonly JsonObject _template;
    private readonly JsonArray _resources;
    private readonly JsonObject _parameters;

    public ArmTemplateBuilder()
    {
        _parameters = new JsonObject();
        _resources = new JsonArray();
        _template = new JsonObject
        {
            ["$schema"] = "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
            ["contentVersion"] = "1.0.0.0",
            ["parameters"] = _parameters,
            ["resources"] = _resources
        };
    }

    public void AddApiConnection(Connector connector)
    {
        if (connector.ManagedApiId is null) return;

        var connectionName = $"{connector.ConnectorType.Name.ToLowerInvariant()}-connection";
        var connection = new JsonObject
        {
            ["type"] = "Microsoft.Web/connections",
            ["apiVersion"] = "2016-06-01",
            ["name"] = connectionName,
            ["location"] = "[resourceGroup().location]",
            ["properties"] = new JsonObject
            {
                ["api"] = new JsonObject
                {
                    ["id"] = $"[subscriptionResourceId('Microsoft.Web/locations/managedApis', resourceGroup().location, '{connector.ConnectorType.Name.ToLowerInvariant()}')]"
                }
            }
        };

        _resources.Add(connection);
    }

    public void AddLogicAppWorkflow(string workflowName, JsonObject actions, JsonObject? connections = null)
    {
        var definition = new JsonObject
        {
            ["$schema"] = "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
            ["contentVersion"] = "1.0.0.0",
            ["triggers"] = new JsonObject
            {
                ["manual"] = new JsonObject { ["type"] = "Request", ["kind"] = "Http" }
            },
            ["actions"] = actions
        };

        var properties = new JsonObject { ["definition"] = definition };
        if (connections is not null)
            properties["parameters"] = new JsonObject { ["$connections"] = new JsonObject { ["value"] = connections } };

        var logicApp = new JsonObject
        {
            ["type"] = "Microsoft.Logic/workflows",
            ["apiVersion"] = "2019-05-01",
            ["name"] = workflowName,
            ["location"] = "[resourceGroup().location]",
            ["properties"] = properties
        };

        _resources.Add(logicApp);
    }

    public string Build() => _template.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
