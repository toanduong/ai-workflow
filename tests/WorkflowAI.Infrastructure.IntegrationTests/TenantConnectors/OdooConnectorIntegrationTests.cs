using Anthropic.SDK;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;
using WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;
using WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.AI;
using WorkflowAI.Infrastructure.Connectors;
using WorkflowAI.Infrastructure.Connectors.Auth;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Real integration tests — uses actual PostgreSQL + actual Claude API.
///
/// Prerequisites:
///   1. Docker postgres running:  docker-compose up -d postgres
///   2. Anthropic API key set in src/WorkflowAI.Functions/local.settings.json
///      under Anthropic:ApiKey  (or env var ANTHROPIC_API_KEY)
///   3. (Optional) For validate test: set ODOO_INSTANCE_URL + ODOO_API_KEY env vars
/// </summary>
public class OdooConnectorIntegrationTests : IAsyncLifetime
{
    // ── Config — reads local.settings.json, falls back to env vars ────────────
    private static class TestConfig
    {
        private static readonly Lazy<JsonDocument?> _localSettings = new(() =>
        {
            // Walk up from test binary to find local.settings.json
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var file = Path.Combine(dir.FullName,
                    "src", "WorkflowAI.Functions", "local.settings.json");
                if (File.Exists(file))
                    return JsonDocument.Parse(File.ReadAllText(file));
                dir = dir.Parent;
            }
            return null;
        });

        private static string? LocalSetting(string path)
        {
            try
            {
                var doc = _localSettings.Value;
                if (doc is null) return null;
                var parts = path.Split(':');
                JsonElement el = doc.RootElement;
                foreach (var part in parts)
                    if (!el.TryGetProperty(part, out el)) return null;
                return el.GetString();
            }
            catch { return null; }
        }

        public static string ConnectionString =>
            Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? LocalSetting("ConnectionStrings:PostgreSql")
            ?? "Host=localhost;Port=5433;Database=workflowai_dev;Username=postgres;Password=devpassword";

        public static string AnthropicApiKey =>
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? LocalSetting("Anthropic:ApiKey")
            ?? string.Empty;

        /// <summary>
        /// A JSON object whose keys match the connector's Claude-generated requiredFields.
        /// Set CONNECTOR_CREDENTIALS env var or local.settings.json "Connector:Credentials".
        ///
        /// Examples:
        ///   Apollo (SaaS, fixed URL):
        ///     { "api_key": "your-apollo-key" }
        ///
        ///   Odoo (self-hosted, URL per tenant):
        ///     { "api_key": "your-odoo-key", "instance_url": "https://mycompany.odoo.com" }
        ///
        ///   Chatwoot (self-hosted):
        ///     { "access_token": "your-token", "base_url": "https://chatwoot.mycompany.com" }
        /// </summary>
        public static IReadOnlyDictionary<string, string>? ConnectorCredentials()
        {
            var json = Environment.GetEnvironmentVariable("CONNECTOR_CREDENTIALS")
                       ?? LocalSetting("Connector:Credentials");
            if (json is null) return null;
            try { return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json); }
            catch { return null; }
        }

        /// <summary>Which connector name to test the full flow against.</summary>
        public static string ConnectorName =>
            Environment.GetEnvironmentVariable("CONNECTOR_NAME")
            ?? LocalSetting("Connector:Name")
            ?? "Odoo";
    }

    // ── Real dependencies ──────────────────────────────────────────────────
    private WorkflowAIDbContext _db = null!;
    private SqlTenantConnectorRepository _repository = null!;
    private AnthropicService _anthropicService = null!;

    // ── Setup ─────────────────────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        // Real PostgreSQL
        var options = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(TestConfig.ConnectionString)
            .Options;
        _db = new WorkflowAIDbContext(options);
        _repository = new SqlTenantConnectorRepository(_db);

        // Real Anthropic Claude
        var anthropicOptions = Options.Create(new AnthropicOptions
        {
            ApiKey = TestConfig.AnthropicApiKey,
            DefaultModel = "claude-sonnet-4-5"
        });
        IChatClient chatClient = new AnthropicClient(
            apiKeys: new APIAuthentication(TestConfig.AnthropicApiKey)).Messages;
        _anthropicService = new AnthropicService(
            chatClient, anthropicOptions,
            NullLogger<AnthropicService>.Instance);

        await Task.CompletedTask;
    }

    // ── Teardown: data is kept in the database for inspection ─────────────
    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: Provision — real Claude generates metadata for any connector name
    // ─────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("Odoo")]
    [InlineData("Apollo")]
    [InlineData("HubSpot")]
    [InlineData("Chatwoot")]
    [InlineData("Salesforce")]
    public async Task Provision_ShouldSaveClaudeGeneratedMetadataToDatabase(string connectorName)
    {
        SkipIfNoApiKey();

        var tenantId = Guid.NewGuid();
        var handler = new ProvisionTenantConnectorCommandHandler(_repository, _anthropicService);

        // Act — calls REAL Claude API; no code change needed per connector
        var result = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, connectorName),
            CancellationToken.None);

        // Assert — result is generic: we only care that Claude returned structured metadata
        result.IsSuccess.Should().BeTrue(
            $"Claude should return valid metadata for '{connectorName}', " +
            $"but got error: {result.Error?.Code} — {result.Error?.Message}");
        result.Value!.ConnectorName.Should().Be(connectorName);
        result.Value.Metadata.Should().NotBeNullOrEmpty();
        result.Value.Info.Should().NotBeNullOrEmpty();

        // Metadata must contain the generic schema fields
        result.Value.Metadata.Should().ContainAny("APIKey", "OAuth2", "Basic", "Bearer");
        result.Value.Metadata.Should().Contain("testEndpoint");
        result.Value.Metadata.Should().Contain("baseUrl");

        // Assert — actually saved to PostgreSQL
        var saved = await _repository.GetByIdAsync(
            TenantConnectorId.From(result.Value.TenantConnectorId));
        saved.Should().NotBeNull();
        saved!.ConnectorName.Should().Be(connectorName);
        saved.Status.Should().Be(TenantConnectorStatus.Pending);
        saved.Metadata.Should().Be(result.Value.Metadata);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: Provision twice — conflict check (connector name is arbitrary)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Provision_Twice_ShouldReturnConflict()
    {
        SkipIfNoApiKey();

        var tenantId = Guid.NewGuid();
        var handler = new ProvisionTenantConnectorCommandHandler(_repository, _anthropicService);

        var first = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, "Apollo"),
            CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // Same tenant + same connector name → conflict
        var second = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, "Apollo"),
            CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error!.Code.Should().Be("TenantConnector.AlreadyExists");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: Full flow — Provision + Validate (requires real Odoo instance)
    //         Set ODOO_INSTANCE_URL + ODOO_API_KEY env vars to run this test
    // ─────────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: Full flow — Provision + Validate (requires real connector credentials)
    //
    // Configure via environment variables or local.settings.json:
    //   CONNECTOR_NAME        → connector to test (e.g. "Odoo", "Apollo", "HubSpot")
    //   CONNECTOR_CREDENTIALS → JSON object with keys matching requiredFields, e.g.
    //                           Apollo:   {"api_key":"your-key"}
    //                           Odoo:     {"api_key":"your-key","instance_url":"https://mycompany.odoo.com"}
    //                           Chatwoot: {"access_token":"your-token","base_url":"https://chatwoot.example.com"}
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FullFlow_Provision_Then_Validate_Connector()
    {
        SkipIfNoApiKey();

        var credentials = TestConfig.ConnectorCredentials();
        if (credentials is null || credentials.Count == 0)
        {
            // Skip gracefully — no connector credentials configured
            return;
        }

        var connectorName = TestConfig.ConnectorName;
        var tenantId = Guid.NewGuid();

        // Step 1 — Provision: Claude generates metadata + requiredFields for this connector
        var provisionHandler = new ProvisionTenantConnectorCommandHandler(
            _repository, _anthropicService);

        var provision = await provisionHandler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, connectorName),
            CancellationToken.None);

        provision.IsSuccess.Should().BeTrue(
            $"Provision failed: {provision.Error?.Code} — {provision.Error?.Message}");
        var connectorId = provision.Value!.TenantConnectorId;

        // Step 2 — Validate: test real HTTP connection
        // Credentials are passed as-is; the handler resolves baseUrl and auth from Claude-generated metadata.
        ICredentialApplicatorFactory applicatorFactory = new CredentialApplicatorFactory(
        [
            new ApiKeyCredentialApplicator(),
            new BearerCredentialApplicator(),
            new OAuth2CredentialApplicator(),
            new BasicCredentialApplicator(),
            new DefaultCredentialApplicator()
        ]);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(true);

        var validateHandler = new ValidateTenantConnectorCommandHandler(
            _repository, applicatorFactory, new NoOpKeyVaultService(), currentUser,
            NullLogger<ValidateTenantConnectorCommandHandler>.Instance,
            new HttpClient());

        var validate = await validateHandler.Handle(
            new ValidateTenantConnectorCommand(tenantId, connectorId, credentials),
            CancellationToken.None);

        validate.IsSuccess.Should().BeTrue();
        validate.Value.Should().BeTrue(
            "Connector credentials should be valid — check CONNECTOR_CREDENTIALS env var");

        // Assert Table 1 status = Active
        var connector = await _repository.GetByIdAsync(TenantConnectorId.From(connectorId));
        connector!.Status.Should().Be(TenantConnectorStatus.Active);

        // Step 3 — GenerateConnectorAssets: Claude discovers API operations → TenantConnectorApis only
        var generateHandler = new GenerateConnectorAssetsCommandHandler(
            _repository, _anthropicService, currentUser,
            NullLogger<GenerateConnectorAssetsCommandHandler>.Instance);

        var generate = await generateHandler.Handle(
            new GenerateConnectorAssetsCommand(connectorId), CancellationToken.None);

        generate.IsSuccess.Should().BeTrue(
            $"GenerateConnectorAssets failed: {generate.Error?.Code} — {generate.Error?.Message}");
        generate.Value!.ApiRoutes.Should().NotBe("[]",
            "Claude should have returned at least one API operation");

        // Assert TenantConnectorApis rows were saved (SRP: operations go here, not WorkflowTemplates)
        var apis = await _repository.GetApisByConnectorAsync(TenantConnectorId.From(connectorId));
        apis.Should().NotBeEmpty("TenantConnectorApis must be populated by GenerateConnectorAssets");
        apis.Should().AllSatisfy(api =>
        {
            api.HttpMethod.Should().NotBeNullOrEmpty();
            api.UrlTemplate.Should().NotBeNullOrEmpty();
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void SkipIfNoApiKey()
    {
        if (string.IsNullOrEmpty(TestConfig.AnthropicApiKey))
            throw new SkipException(
                "Set ANTHROPIC_API_KEY environment variable to run integration tests.");
    }
}

/// <summary>Minimal IHttpClientFactory for integration tests.</summary>
internal sealed class RealHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}

/// <summary>Signals xUnit to skip a test gracefully.</summary>
internal sealed class SkipException(string reason) : Exception(reason);
