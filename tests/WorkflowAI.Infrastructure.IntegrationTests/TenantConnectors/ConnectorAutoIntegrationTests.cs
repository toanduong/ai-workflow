using Anthropic.SDK;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
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
/// Generic end-to-end integration test for ANY connector.
/// Driven entirely by local.settings.json — no code changes needed per connector.
///
/// Prerequisites:
///   1. Docker postgres OR Azure PostgreSQL connection string in local.settings.json
///   2. Anthropic API key in local.settings.json under Anthropic:ApiKey
///   3. Connector config in local.settings.json:
///        "Connector": {
///          "Name": "HubSpot",
///          "Credentials": "{\"access_token\": \"pat-na1-xxx\"}"
///        }
///      → Change Name + Credentials to test a different connector.
///      → Credentials keys must match the requiredFields Claude returns in Provision.
///   4. (Optional) Key Vault URI for real credential storage:
///        "KeyVault": { "Uri": "https://banhanhapp-dev.vault.azure.net/" }
///      → Run `az login` first so DefaultAzureCredential can authenticate.
///      → Without URI, NoOpKeyVaultService is used (credentials not persisted to KV).
///
/// Data is kept in the database after tests run — inspect rows for debugging.
/// </summary>
public class ConnectorAutoIntegrationTests : IAsyncLifetime
{
    // ── Config — reads local.settings.json, falls back to env vars ────────────
    private static class TestConfig
    {
        private static readonly Lazy<JsonDocument?> _localSettings = new(() =>
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var file = Path.Combine(dir.FullName, "src", "WorkflowAI.Functions", "local.settings.json");
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
                JsonElement el = doc.RootElement;
                foreach (var part in path.Split(':'))
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
        /// The connector to test. Change "Connector:Name" in local.settings.json to switch connectors.
        /// </summary>
        public static string ConnectorType =>
            Environment.GetEnvironmentVariable("CONNECTOR_NAME")
            ?? LocalSetting("Connector:Name")
            ?? "Odoo";

        /// <summary>
        /// JSON object whose keys match the connector's Claude-generated requiredFields.
        /// Run Provision first to discover what keys are needed, then fill this in.
        ///
        /// Examples:
        ///   HubSpot:   {"access_token":"pat-na1-xxx"}
        ///   Apollo:    {"api_key":"your-key"}
        ///   Odoo:      {"api_key":"your-key","instance_url":"https://mycompany.odoo.com"}
        ///   Chatwoot:  {"access_token":"your-token","base_url":"https://chatwoot.example.com"}
        /// </summary>
        public static IReadOnlyDictionary<string, string>? ConnectorCredentials()
        {
            // Env var override — flat JSON object e.g. {"access_token":"xxx"}
            var json = Environment.GetEnvironmentVariable("CONNECTOR_CREDENTIALS")
                       // Per-connector entry in local.settings.json: Connector:Credentials:{Name}
                       ?? LocalSetting($"Connector:Credentials:{ConnectorType}");
            if (json is null) return null;
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json); }
            catch { return null; }
        }

        public static string? KeyVaultUri =>
            Environment.GetEnvironmentVariable("KEY_VAULT_URI")
            ?? LocalSetting("KeyVault:Uri");
    }

    // ── Real dependencies ──────────────────────────────────────────────────
    private WorkflowAIDbContext _db = null!;
    private SqlTenantConnectorRepository _repository = null!;
    private AnthropicService _anthropicService = null!;
    private IKeyVaultService _keyVaultService = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(TestConfig.ConnectionString)
            .Options;
        _db = new WorkflowAIDbContext(options);
        _repository = new SqlTenantConnectorRepository(_db);

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

        // Use real Key Vault if URI is configured; otherwise NoOp (credentials not persisted)
        var kvUri = TestConfig.KeyVaultUri;
        _keyVaultService = !string.IsNullOrEmpty(kvUri)
            ? new KeyVaultService(new SecretClient(new Uri(kvUri), new DefaultAzureCredential()))
            : new NoOpKeyVaultService();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: Provision only — verifies Claude generates valid metadata for any connector name.
    //         Run this first to discover what requiredFields the connector needs,
    //         then fill in local.settings.json Connector:Credentials accordingly.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Step1_Provision_ReturnsRequiredFieldsAndSavesToDatabase()
    {
        SkipIfNoApiKey();

        var tenantId = Guid.NewGuid();
        var connectorName = TestConfig.ConnectorType;
        var handler = new ProvisionTenantConnectorCommandHandler(_repository, _anthropicService);

        var result = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, connectorName),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            $"Claude should return valid metadata for '{connectorName}': " +
            $"{result.Error?.Code} — {result.Error?.Message}");

        result.Value!.ConnectorType.Should().Be(connectorName);
        result.Value.Metadata.Should().NotBeNullOrEmpty();
        result.Value.Info.Should().NotBeNullOrEmpty();

        // Metadata must contain generic schema fields
        result.Value.Metadata.Should().ContainAny("APIKey", "OAuth2", "Basic", "Bearer");
        result.Value.Metadata.Should().Contain("testEndpoint");
        result.Value.Metadata.Should().Contain("baseUrl");
        result.Value.Metadata.Should().Contain("requiredFields");

        var saved = await _repository.GetByIdAsync(
            TenantConnectorId.From(result.Value.TenantConnectorId));
        saved.Should().NotBeNull();
        saved!.ConnectorType.Should().Be(connectorName);
        saved.Status.Should().Be(TenantConnectorStatus.Pending);

        // Print requiredFields so you know what to put in Connector:Credentials
        var metadata = JsonDocument.Parse(saved.Metadata);
        if (metadata.RootElement.TryGetProperty("requiredFields", out var fields))
            Console.WriteLine($"\n>>> requiredFields for {connectorName}: {fields}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: Provision twice with same tenant → conflict
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Step1_Provision_Twice_ShouldReturnConflict()
    {
        SkipIfNoApiKey();

        var tenantId = Guid.NewGuid();
        var connectorName = TestConfig.ConnectorType;
        var handler = new ProvisionTenantConnectorCommandHandler(_repository, _anthropicService);

        var first = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, connectorName),
            CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, connectorName),
            CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error!.Code.Should().Be("TenantConnector.AlreadyExists");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: Full flow — Provision → Validate → GenerateAssets
    //
    // Configure local.settings.json:
    //   "Connector": {
    //     "Name": "HubSpot",
    //     "Credentials": "{\"access_token\":\"pat-na1-xxx\"}"
    //   }
    //
    // If Connector:Credentials is not set, this test is skipped gracefully.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FullFlow_Provision_Validate_GenerateAssets()
    {
        SkipIfNoApiKey();

        var credentials = TestConfig.ConnectorCredentials();
        if (credentials is null || credentials.Count == 0)
        {
            Console.WriteLine(
                "Skipping full flow — set Connector:Credentials in local.settings.json. " +
                "Run Step1_Provision_ReturnsRequiredFieldsAndSavesToDatabase first to discover requiredFields.");
            return;
        }

        var connectorName = TestConfig.ConnectorType;
        var tenantId = Guid.NewGuid();

        // ── Step 1: Provision ─────────────────────────────────────────────
        // Claude generates: authType, baseUrl, testEndpoint, requiredFields, configSchema
        var provisionHandler = new ProvisionTenantConnectorCommandHandler(_repository, _anthropicService);

        var provision = await provisionHandler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, connectorName),
            CancellationToken.None);

        provision.IsSuccess.Should().BeTrue(
            $"Provision failed: {provision.Error?.Code} — {provision.Error?.Message}");

        var connectorId = provision.Value!.TenantConnectorId;

        // Print what Claude decided — useful for debugging
        Console.WriteLine($"\n>>> Connector: {connectorName}");
        Console.WriteLine($">>> Metadata: {provision.Value.Metadata}");
        Console.WriteLine($">>> requiredFields filled: {JsonSerializer.Serialize(credentials.Keys)}");

        // ── Step 2: Validate ──────────────────────────────────────────────
        // User-supplied credentials are tested against the real API.
        // On success: credentials stored in Key Vault, connector status → Active.
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
            _repository, applicatorFactory, _keyVaultService, currentUser,
            NullLogger<ValidateTenantConnectorCommandHandler>.Instance,
            new HttpClient());

        var validate = await validateHandler.Handle(
            new ValidateTenantConnectorCommand(tenantId, connectorId, credentials),
            CancellationToken.None);

        validate.IsSuccess.Should().BeTrue(
            $"Validation failed — check Connector:Credentials in local.settings.json");
        validate.Value.Should().BeTrue(
            "HTTP test call to the connector API returned non-2xx — credentials may be invalid");

        // Connector is now Active; CredentialSecretNames mapped to Key Vault
        var activeConnector = await _repository.GetByIdAsync(TenantConnectorId.From(connectorId));
        activeConnector!.Status.Should().Be(TenantConnectorStatus.Active);
        activeConnector.CredentialSecretNames.Should().NotBeNullOrEmpty(
            "Secret names must be persisted so workflow steps can resolve credentials at runtime");

        Console.WriteLine($">>> CredentialSecretNames: {activeConnector.CredentialSecretNames}");

        // ── Step 3: Generate Assets ───────────────────────────────────────
        // Claude reads stored metadata + info → discovers all API operations.
        // Each operation (method + path) is saved to TenantConnectorApis.
        var options = Options.Create(new ConnectorAssetGenerationOptions());
        var generateHandler = new GenerateConnectorAssetsCommandHandler(
            _repository, _anthropicService, currentUser, options,
            NullLogger<GenerateConnectorAssetsCommandHandler>.Instance);

        var generate = await generateHandler.Handle(
            new GenerateConnectorAssetsCommand(connectorId), CancellationToken.None);

        generate.IsSuccess.Should().BeTrue(
            $"GenerateConnectorAssets failed: {generate.Error?.Code} — {generate.Error?.Message}");
        generate.Value!.ApiRoutes.Should().NotBe("[]",
            "Claude should discover at least one API operation");

        // TenantConnectorApis rows must be saved
        var apis = await _repository.GetApisByConnectorAsync(TenantConnectorId.From(connectorId));
        apis.Should().NotBeEmpty("TenantConnectorApis must be populated");
        apis.Should().AllSatisfy(api =>
        {
            api.HttpMethod.Should().NotBeNullOrEmpty();
            api.UrlTemplate.Should().NotBeNullOrEmpty();
        });

        Console.WriteLine($">>> Discovered {apis.Count} API operations for {connectorName}:");
        foreach (var api in apis.Take(10))
            Console.WriteLine($"    {api.HttpMethod} {api.UrlTemplate}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void SkipIfNoApiKey()
    {
        if (string.IsNullOrEmpty(TestConfig.AnthropicApiKey))
            throw new SkipException(
                "Set ANTHROPIC_API_KEY or Anthropic:ApiKey in local.settings.json to run integration tests.");
    }
}

/// <summary>Signals xUnit to skip a test gracefully.</summary>
internal sealed class SkipException(string reason) : Exception(reason);
