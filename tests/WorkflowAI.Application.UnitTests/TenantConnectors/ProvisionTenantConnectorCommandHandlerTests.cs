using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.TenantConnectors;

public class ProvisionTenantConnectorCommandHandlerTests
{
    private readonly ITenantConnectorRepository _repository =
        Substitute.For<ITenantConnectorRepository>();
    private readonly IAnthropicService _anthropicService =
        Substitute.For<IAnthropicService>();

    private ProvisionTenantConnectorCommandHandler CreateHandler() =>
        new(_repository, _anthropicService);

    // Generic Claude response — no connector-specific knowledge in the fixture.
    // Real Claude would fill in the actual values per connector name.
    private static readonly string ValidClaudeResponse = """
        {
          "metadata": {
            "authType": "APIKey",
            "requiredFields": ["api_key"],
            "baseUrl": "https://api.example.com",
            "testEndpoint": { "method": "GET", "path": "/health" },
            "configSchema": {
              "api_key": { "type": "string", "description": "API key", "required": true }
            }
          },
          "info": {
            "description": "A third-party integration service",
            "docsUrl": "https://docs.example.com/api",
            "capabilities": ["read", "write", "webhooks"],
            "rateLimits": "1000 requests/hour",
            "webhookSupport": true
          }
        }
        """;

    // Self-hosted variant — Claude uses a {placeholder} in baseUrl
    private static readonly string SelfHostedClaudeResponse = """
        {
          "metadata": {
            "authType": "APIKey",
            "requiredFields": ["api_key", "instance_url"],
            "baseUrl": "{instance_url}",
            "testEndpoint": { "method": "GET", "path": "/health" },
            "configSchema": {
              "api_key":      { "type": "string", "description": "API key", "required": true },
              "instance_url": { "type": "string", "description": "Your instance URL, e.g. https://mycompany.example.com", "required": true }
            }
          },
          "info": {
            "description": "A self-hosted integration platform",
            "docsUrl": "https://docs.example.com/api",
            "capabilities": ["contacts", "orders", "inventory"],
            "rateLimits": "unlimited",
            "webhookSupport": false
          }
        }
        """;

    [Fact]
    public async Task Handle_ShouldProvisionConnector_WhenClaudeReturnsValidJson()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetByTenantAndNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidClaudeResponse, 300, true));

        var command = new ProvisionTenantConnectorCommand(tenantId, "Apollo");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ConnectorType.Should().Be("Apollo");
        result.Value.Metadata.Should().Contain("APIKey");
        result.Value.Metadata.Should().Contain("testEndpoint");
        result.Value.Info.Should().Contain("docs.example.com");
        await _repository.Received(1).AddAsync(
            Arg.Is<TenantConnector>(c =>
                c.ConnectorType == "Apollo" &&
                c.Status == TenantConnectorStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSavePlaceholderBaseUrl_ForSelfHostedConnectors()
    {
        _repository.GetByTenantAndNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(SelfHostedClaudeResponse, 300, true));

        var command = new ProvisionTenantConnectorCommand(Guid.NewGuid(), "SelfHostedERP");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // The {instance_url} placeholder must be preserved as-is in stored Metadata
        result.Value!.Metadata.Should().Contain("{instance_url}");
        result.Value.Metadata.Should().Contain("instance_url"); // also in requiredFields
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenConnectorAlreadyExists()
    {
        var tenantId = Guid.NewGuid();
        var existing = new TenantConnectorBuilder().WithConnectorType("Apollo").Build();
        _repository.GetByTenantAndNameAsync(Arg.Any<TenantId>(), "Apollo", Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new ProvisionTenantConnectorCommand(tenantId, "Apollo");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.AlreadyExists");
        await _anthropicService.DidNotReceive()
            .CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenClaudeFails()
    {
        _repository.GetByTenantAndNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 0, false, "Anthropic API timeout"));

        var result = await CreateHandler().Handle(
            new ProvisionTenantConnectorCommand(Guid.NewGuid(), "Apollo"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.AiFailed");
        await _repository.DidNotReceive().AddAsync(Arg.Any<TenantConnector>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenClaudeReturnsInvalidJson()
    {
        _repository.GetByTenantAndNameAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult("not valid json at all", 50, true));

        var result = await CreateHandler().Handle(
            new ProvisionTenantConnectorCommand(Guid.NewGuid(), "Apollo"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.InvalidAiResponse");
    }

    [Theory]
    [InlineData("Apollo")]
    [InlineData("Salesforce")]
    [InlineData("HubSpot")]
    [InlineData("Shopify")]
    [InlineData("Stripe")]
    [InlineData("Chatwoot")]
    [InlineData("Zalo")]
    [InlineData("Odoo")]
    [InlineData("MySQL")]
    [InlineData("GoogleSheets")]
    public async Task Handle_ShouldWork_ForAnyConnectorType_WithoutCodeChanges(string connectorName)
    {
        _repository.GetByTenantAndNameAsync(
            Arg.Any<TenantId>(), connectorName, Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidClaudeResponse, 300, true));

        var command = new ProvisionTenantConnectorCommand(Guid.NewGuid(), connectorName);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue($"connector '{connectorName}' should provision without code changes");
        result.Value!.ConnectorType.Should().Be(connectorName);
    }
}
