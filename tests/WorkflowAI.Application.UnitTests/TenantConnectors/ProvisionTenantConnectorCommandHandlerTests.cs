using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.TenantConnectors;

public class ProvisionTenantConnectorCommandHandlerTests
{
    private readonly ITenantConnectorRepository _repository = Substitute.For<ITenantConnectorRepository>();
    private readonly IClaudeAIService _claudeAIService = Substitute.For<IClaudeAIService>();

    private static readonly string ValidClaudeResponse = """
        {
          "metadata": {
            "authType": "APIKey",
            "requiredFields": ["api_key"],
            "testEndpoint": {"method": "GET", "path": "https://api.apollo.io/v1/auth/health"},
            "endpoints": {"getContact": {"method": "GET", "path": "/people/match"}},
            "configSchema": {"type": "object", "properties": {"api_key": {"type": "string"}}}
          },
          "info": {
            "description": "Apollo.io is a sales intelligence and engagement platform.",
            "docsUrl": "https://apolloio.github.io/apollo-api-docs/",
            "capabilities": ["contact_search", "email_sequences", "crm_sync"],
            "rateLimits": "50 requests/minute on free tier",
            "webhookSupport": true
          }
        }
        """;

    private ProvisionTenantConnectorCommandHandler CreateHandler() =>
        new(_repository, _claudeAIService);

    [Fact]
    public async Task Handle_NewApolloConnector_ReturnsMataAndInfo()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetByTenantAndConnectorAsync(
            TenantId.From(tenantId), "Apollo", Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        _claudeAIService.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidClaudeResponse, 500, true));

        var command = new ProvisionTenantConnectorCommand(tenantId, "Apollo");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TenantConnectorId.Should().NotBeEmpty();
        result.Value.Metadata.Should().Contain("APIKey");
        result.Value.Info.Should().Contain("Apollo.io");

        await _repository.Received(1).AddAsync(
            Arg.Is<TenantConnector>(c =>
                c.ConnectorName == "Apollo" &&
                c.TenantId == TenantId.From(tenantId) &&
                c.Status == TenantConnectorStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingConnector_ReturnsConflictError()
    {
        var tenantId = Guid.NewGuid();
        var existing = new Common.Builders.TenantConnectorBuilder()
            .WithTenantId(tenantId)
            .WithConnectorName("Apollo")
            .Build();

        _repository.GetByTenantAndConnectorAsync(
            TenantId.From(tenantId), "Apollo", Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new ProvisionTenantConnectorCommand(tenantId, "Apollo");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.AlreadyExists");
        await _claudeAIService.DidNotReceive().CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<TenantConnector>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClaudeFails_ReturnsFailureError()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetByTenantAndConnectorAsync(
            TenantId.From(tenantId), "Apollo", Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        _claudeAIService.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 0, false, "Rate limit exceeded"));

        var command = new ProvisionTenantConnectorCommand(tenantId, "Apollo");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.AIGenerationFailed");
        await _repository.DidNotReceive().AddAsync(Arg.Any<TenantConnector>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClaudeReturnsInvalidJson_ReturnsInvalidResponseError()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetByTenantAndConnectorAsync(
            TenantId.From(tenantId), "Apollo", Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        _claudeAIService.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult("not valid json at all", 50, true));

        var command = new ProvisionTenantConnectorCommand(tenantId, "Apollo");
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.InvalidAIResponse");
    }

    [Fact]
    public async Task Handle_DifferentConnectors_ProvisionedIndependently()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetByTenantAndConnectorAsync(Arg.Any<TenantId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        _claudeAIService.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidClaudeResponse, 500, true));

        var apolloResult = await CreateHandler().Handle(
            new ProvisionTenantConnectorCommand(tenantId, "Apollo"), CancellationToken.None);

        var salesforceResult = await CreateHandler().Handle(
            new ProvisionTenantConnectorCommand(tenantId, "Salesforce"), CancellationToken.None);

        apolloResult.IsSuccess.Should().BeTrue();
        salesforceResult.IsSuccess.Should().BeTrue();
        apolloResult.Value!.TenantConnectorId.Should().NotBe(salesforceResult.Value!.TenantConnectorId);
    }
}
