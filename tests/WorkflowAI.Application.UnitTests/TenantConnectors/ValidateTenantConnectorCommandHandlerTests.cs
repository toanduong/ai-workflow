using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.TenantConnectors;

public class ValidateTenantConnectorCommandHandlerTests
{
    private readonly ITenantConnectorRepository _repository =
        Substitute.For<ITenantConnectorRepository>();
    private readonly IAnthropicService _anthropicService =
        Substitute.For<IAnthropicService>();
    private readonly IConnectorHttpValidator _httpValidator =
        Substitute.For<IConnectorHttpValidator>();

    private ValidateTenantConnectorCommandHandler CreateHandler() =>
        new(_repository, _anthropicService, _httpValidator,
            NullLogger<ValidateTenantConnectorCommandHandler>.Instance);

    // ── Claude-generated metadata fixtures ────────────────────────────────────
    //
    // These JSON blobs represent what Claude returns during Provision.
    // The application code never decides what fields are needed — Claude does.
    // The frontend reads requiredFields and builds the form; the admin fills it in.
    // The credentials dict the admin submits always matches requiredFields exactly.

    // Claude returned a fixed baseUrl — admin only fills auth field(s)
    private const string MetadataWithFixedBaseUrl = """
        {
          "authType": "APIKey",
          "baseUrl": "https://api.example.com",
          "testEndpoint": { "method": "GET", "path": "/health" },
          "requiredFields": ["api_key"]
        }
        """;

    // Claude returned a {placeholder} baseUrl — admin fills auth + instance URL
    // This happens when the service is tenant-hosted (each customer has their own URL)
    private const string MetadataWithTemplatedBaseUrl = """
        {
          "authType": "APIKey",
          "baseUrl": "{instance_url}",
          "testEndpoint": { "method": "GET", "path": "/health" },
          "requiredFields": ["api_key", "instance_url"]
        }
        """;

    // ── Admin credential submissions ──────────────────────────────────────────
    //
    // Keys always match Claude's requiredFields — the frontend enforces this by
    // rendering exactly one input per requiredField and using the field name as the key.

    // Admin filled the single-field form (api_key only)
    private static IReadOnlyDictionary<string, string> SingleFieldCredentials() =>
        new Dictionary<string, string> { ["api_key"] = "valid-api-key" };

    // Admin filled the two-field form (api_key + instance_url)
    private static IReadOnlyDictionary<string, string> TwoFieldCredentials() =>
        new Dictionary<string, string>
        {
            ["api_key"]      = "valid-api-key",
            ["instance_url"] = "https://mycompany.example.com"
        };

    // ── Discovered API operations (Claude's response during Validate) ─────────

    private static readonly string ValidApiDiscoveryResponse = """
        [
          {
            "apiName": "contacts/list",
            "httpMethod": "GET",
            "urlTemplate": "https://api.example.com/v1/contacts",
            "metadata": { "headers": { "X-Api-Key": "$secret" } }
          },
          {
            "apiName": "orders/create",
            "httpMethod": "POST",
            "urlTemplate": "https://api.example.com/v1/orders",
            "metadata": { "headers": { "X-Api-Key": "$secret" } }
          },
          {
            "apiName": "products/list",
            "httpMethod": "GET",
            "urlTemplate": "https://api.example.com/v1/products",
            "metadata": { "headers": { "X-Api-Key": "$secret" } }
          }
        ]
        """;

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenConnectorDoesNotExist()
    {
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        var result = await CreateHandler().Handle(
            new ValidateTenantConnectorCommand(Guid.NewGuid(), SingleFieldCredentials()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldActivateAndDiscoverApis_WhenConnectionSucceeds()
    {
        // Connector whose Claude-generated metadata has a fixed baseUrl
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .WithMetadata(MetadataWithFixedBaseUrl, "{}")
            .Build();

        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _httpValidator.TestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, (string?)null));
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidApiDiscoveryResponse, 500, true));

        var result = await CreateHandler().Handle(
            new ValidateTenantConnectorCommand(connector.Id.Value, SingleFieldCredentials()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.ApiOperationsDiscovered.Should().Be(3);
        connector.Status.Should().Be(TenantConnectorStatus.Active);
        await _repository.Received(1).AddApisAsync(
            Arg.Is<IEnumerable<TenantConnectorApi>>(apis => apis.Count() == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSubstitutePlaceholderInBaseUrl_WhenClaudeGeneratedTemplate()
    {
        // Claude generated "{instance_url}" in baseUrl because this connector is tenant-hosted.
        // The frontend read requiredFields = ["api_key","instance_url"], rendered two form fields,
        // and the admin filled both. The credentials dict is what the frontend collected.
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("TenantHostedService")
            .WithMetadata(MetadataWithTemplatedBaseUrl, "{}")
            .Build();

        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _httpValidator.TestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, (string?)null));
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult("[]", 100, true));

        await CreateHandler().Handle(
            new ValidateTenantConnectorCommand(connector.Id.Value, TwoFieldCredentials()),
            CancellationToken.None);

        // The handler must substitute {instance_url} with the credential value before probing
        await _httpValidator.Received(1).TestAsync(
            Arg.Is<string>(url => url.StartsWith("https://mycompany.example.com")),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldMarkFailed_WhenConnectionReturns401()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("HubSpot")
            .WithMetadata(MetadataWithFixedBaseUrl, "{}")
            .Build();

        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _httpValidator.TestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((false, "HTTP 401: Unauthorized"));

        var result = await CreateHandler().Handle(
            new ValidateTenantConnectorCommand(connector.Id.Value, SingleFieldCredentials()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.FailureReason.Should().Contain("401");
        connector.Status.Should().Be(TenantConnectorStatus.Failed);
        // Claude must NOT be called for API discovery if the HTTP test failed
        await _anthropicService.DidNotReceive()
            .CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDeleteOldApis_BeforeAddingNew_WhenRevalidating()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Stripe")
            .WithMetadata(MetadataWithFixedBaseUrl, "{}")
            .Activated()
            .Build();

        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _httpValidator.TestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, (string?)null));
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidApiDiscoveryResponse, 500, true));

        await CreateHandler().Handle(
            new ValidateTenantConnectorCommand(connector.Id.Value, SingleFieldCredentials()),
            CancellationToken.None);

        await _repository.Received(1).DeleteApisByConnectorAsync(
            connector.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStillActivate_WhenClaudeApiDiscoveryFails()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Salesforce")
            .WithMetadata(MetadataWithFixedBaseUrl, "{}")
            .Build();

        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _httpValidator.TestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, (string?)null));
        _anthropicService.CompleteAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 0, false, "timeout"));

        var result = await CreateHandler().Handle(
            new ValidateTenantConnectorCommand(connector.Id.Value, SingleFieldCredentials()),
            CancellationToken.None);

        // Connection succeeded — connector is Active even if Claude timed out for API discovery
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value!.ApiOperationsDiscovered.Should().Be(0);
        connector.Status.Should().Be(TenantConnectorStatus.Active);
    }
}
