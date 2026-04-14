using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WorkflowAI.Infrastructure.Azure;

namespace WorkflowAI.Infrastructure.IntegrationTests.Azure;

/// <summary>
/// Integration tests for AzureDeploymentService.
/// These tests deploy ARM templates to a real Azure subscription.
/// 
/// Prerequisites:
/// 1. Run 'az login' to authenticate
/// 2. Ensure you have Contributor permissions on the target resource group
/// 3. Update local.settings.json with correct subscription/resource group
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Azure")]
public class AzureDeploymentServiceTests : IAsyncLifetime
{
    private readonly AzureDeploymentService _deploymentService;
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private readonly string _location = "southeastasia";
    private readonly string _testDeploymentName;
    private readonly string _testLogicAppName;
    private readonly IConfiguration _configuration;

    public AzureDeploymentServiceTests()
    {
        // Load configuration from local.settings.json
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: false)
            .Build();

        _subscriptionId = _configuration["AzureResources:SubscriptionId"] 
            ?? throw new InvalidOperationException("AzureResources:SubscriptionId not found in local.settings.json");
        
        _resourceGroupName = _configuration["AzureResources:ResourceGroupName"] 
            ?? throw new InvalidOperationException("AzureResources:ResourceGroupName not found in local.settings.json");

        // Generate unique names for this test run to avoid conflicts
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        _testDeploymentName = $"test-deployment-{timestamp}";
        _testLogicAppName = $"test-logic-app-{timestamp}";

        // Setup deployment service with real Azure credentials
        var credential = new DefaultAzureCredential();
        var logger = Substitute.For<ILogger<AzureDeploymentService>>();

        _deploymentService = new AzureDeploymentService(credential, logger);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Cleanup: Delete the test Logic App after tests complete
        try
        {
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);
            
            var subscriptionResource = armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{_subscriptionId}"));
            
            var resourceGroupResource = await subscriptionResource
                .GetResourceGroups()
                .GetAsync(_resourceGroupName);

            // Delete Logic App
            var logicAppId = new ResourceIdentifier(
                $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Logic/workflows/{_testLogicAppName}");
            
            var logicAppResource = armClient.GetGenericResource(logicAppId);
            await logicAppResource.DeleteAsync(WaitUntil.Completed);

            Console.WriteLine($"✅ Cleaned up test Logic App: {_testLogicAppName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Cleanup warning: {ex.Message}");
            // Don't fail test if cleanup fails
        }
    }

    [Fact]
    public async Task DeployArmTemplateAsync_ShouldSucceed_WithValidTemplate()
    {
        // Arrange
        var armTemplateJson = await File.ReadAllTextAsync(
            Path.Combine(Directory.GetCurrentDirectory(), "Azure", "SampleArmTemplate.json"));

        var parameters = new Dictionary<string, string>
        {
            ["logicAppName"] = _testLogicAppName,
            ["location"] = _location
        };

        // Act
        var result = await _deploymentService.DeployArmTemplateAsync(
            _subscriptionId,
            _resourceGroupName,
            _testDeploymentName,
            armTemplateJson,
            parameters,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue($"Deployment should succeed. Error: {result.ErrorMessage}");
        result.Status.Should().Be("Succeeded");
        result.DeploymentId.Should().NotBeNullOrEmpty();
        result.LogicAppResourceId.Should().NotBeNullOrEmpty();
        result.LogicAppResourceId.Should().Contain(_testLogicAppName);
        result.LogicAppUrl.Should().NotBeNullOrEmpty();
        result.LogicAppUrl.Should().Contain("portal.azure.com");
        result.ErrorMessage.Should().BeNull();

        Console.WriteLine($"✅ Deployment succeeded:");
        Console.WriteLine($"   DeploymentId: {result.DeploymentId}");
        Console.WriteLine($"   LogicAppResourceId: {result.LogicAppResourceId}");
        Console.WriteLine($"   LogicAppUrl: {result.LogicAppUrl}");
    }

    [Fact]
    public async Task DeployArmTemplateAsync_ShouldReturnError_WithInvalidTemplate()
    {
        // Arrange - Invalid ARM template (missing required fields)
        var invalidArmTemplate = """
        {
          "$schema": "invalid-schema",
          "contentVersion": "1.0.0.0"
        }
        """;

        var parameters = new Dictionary<string, string>();

        // Act
        var result = await _deploymentService.DeployArmTemplateAsync(
            _subscriptionId,
            _resourceGroupName,
            $"{_testDeploymentName}-invalid",
            invalidArmTemplate,
            parameters,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be("Failed");
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.LogicAppResourceId.Should().BeNull();

        Console.WriteLine($"✅ Invalid template correctly rejected:");
        Console.WriteLine($"   Error: {result.ErrorMessage}");
    }

    [Fact]
    public async Task DeployArmTemplateAsync_ShouldReturnError_WithInvalidResourceGroup()
    {
        // Arrange
        var armTemplateJson = await File.ReadAllTextAsync(
            Path.Combine(Directory.GetCurrentDirectory(), "Azure", "SampleArmTemplate.json"));

        var parameters = new Dictionary<string, string>
        {
            ["logicAppName"] = _testLogicAppName,
            ["location"] = _location
        };

        var invalidResourceGroup = "non-existent-rg-12345";

        // Act
        var result = await _deploymentService.DeployArmTemplateAsync(
            _subscriptionId,
            invalidResourceGroup,
            _testDeploymentName,
            armTemplateJson,
            parameters,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be("Failed");
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ErrorMessage.Should().Contain("ResourceGroupNotFound", 
            because: "Azure should return resource group not found error");

        Console.WriteLine($"✅ Invalid resource group correctly rejected:");
        Console.WriteLine($"   Error: {result.ErrorMessage}");
    }

    [Fact]
    public async Task GetDeploymentStatusAsync_ShouldReturnStatus_ForExistingDeployment()
    {
        // Arrange - First deploy a template
        var armTemplateJson = await File.ReadAllTextAsync(
            Path.Combine(Directory.GetCurrentDirectory(), "Azure", "SampleArmTemplate.json"));

        var parameters = new Dictionary<string, string>
        {
            ["logicAppName"] = _testLogicAppName,
            ["location"] = _location
        };

        var deployResult = await _deploymentService.DeployArmTemplateAsync(
            _subscriptionId,
            _resourceGroupName,
            _testDeploymentName,
            armTemplateJson,
            parameters,
            CancellationToken.None);

        deployResult.Success.Should().BeTrue();

        // Act - Query deployment status
        var status = await _deploymentService.GetDeploymentStatusAsync(
            _subscriptionId,
            _resourceGroupName,
            _testDeploymentName,
            CancellationToken.None);

        // Assert
        status.Status.Should().Be("Succeeded");
        status.LogicAppResourceId.Should().NotBeNullOrEmpty();
        status.LogicAppUrl.Should().NotBeNullOrEmpty();
        status.CompletedAt.Should().NotBeNull();
        status.ErrorMessage.Should().BeNull();

        Console.WriteLine($"✅ Deployment status retrieved:");
        Console.WriteLine($"   Status: {status.Status}");
        Console.WriteLine($"   CompletedAt: {status.CompletedAt}");
    }

    [Fact]
    public async Task GetDeploymentStatusAsync_ShouldReturnError_ForNonExistentDeployment()
    {
        // Arrange
        var nonExistentDeploymentName = "non-existent-deployment-12345";

        // Act
        var status = await _deploymentService.GetDeploymentStatusAsync(
            _subscriptionId,
            _resourceGroupName,
            nonExistentDeploymentName,
            CancellationToken.None);

        // Assert
        status.Status.Should().Be("NotFound");
        status.LogicAppResourceId.Should().BeNull();
        status.ErrorMessage.Should().Be("Deployment not found");

        Console.WriteLine($"✅ Non-existent deployment correctly handled:");
        Console.WriteLine($"   Status: {status.Status}");
    }

    /// <summary>
    /// Manual test - Deploy ARM template với HTTP connector authentication
    /// Requires real connector credentials in ARM template
    /// </summary>
    [Fact(Skip = "Manual test - requires connector credentials")]
    public async Task DeployArmTemplateAsync_WithConnectorAuth_ShouldSucceed()
    {
        // This test would use an ARM template with actual connector parameters
        // (e.g., Odoo API key, instance URL)
        // Skip by default since it requires actual credentials

        Assert.Fail("Not implemented - requires real ARM template with connectors");
    }
}
