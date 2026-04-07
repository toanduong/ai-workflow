using Anthropic.SDK;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.AI;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// End-to-end integration tests for provisioning a connector.
///
/// Requirements:
///   ANTHROPIC_API_KEY  — real Claude API key
///   WORKFLOWAI_CONNECTION_STRING (optional) — Postgres connection string;
///       defaults to the local docker-compose database.
///   CONNECTOR_NAME (optional) — name of the 3rd-party connector to test;
///       defaults to "Apollo".
///
/// What these tests verify end-to-end:
///   1. Claude can generate valid metadata + info JSON for the given connector name
///   2. The row is persisted to the TenantConnectors table
///   3. Reading the row back from the DB returns the same connector name, status,
///      metadata, and required fields as Claude produced
///
/// Cleanup: each test deletes the row it creates so reruns start clean.
/// </summary>
[Trait("Category", "Integration")]
public class ProvisionConnectorEndToEndTests : IAsyncLifetime
{
    private static readonly string AnthropicApiKey =
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set.");

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("WORKFLOWAI_CONNECTION_STRING")
        ?? "Host=localhost;Port=5433;Database=workflowai_dev;Username=postgres;Password=devpassword;Ssl Mode=Disable";

    // The connector under test — override with CONNECTOR_NAME=Salesforce etc.
    private static readonly string ConnectorName =
        Environment.GetEnvironmentVariable("CONNECTOR_NAME") ?? "Apollo";

    private WorkflowAIDbContext _db = null!;
    private SqlTenantConnectorRepository _repository = null!;
    private ProvisionTenantConnectorCommandHandler _handler = null!;

    // Tracks row IDs created during the test run so we can clean up
    private readonly List<TenantConnectorId> _createdIds = [];

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        _db = new WorkflowAIDbContext(options);
        _repository = new SqlTenantConnectorRepository(_db);

        IChatClient chatClient = new AnthropicClient(
            apiKeys: new APIAuthentication(AnthropicApiKey)).Messages;
        var anthropicOptions = Options.Create(new AnthropicOptions
        {
            ApiKey = AnthropicApiKey,
            DefaultModel = "claude-opus-4-6"
        });
        var anthropicService = new AnthropicService(
            chatClient, anthropicOptions, NullLogger<AnthropicService>.Instance);

        _handler = new ProvisionTenantConnectorCommandHandler(
            _repository, anthropicService);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var id in _createdIds)
            await _repository.DeleteAsync(id);

        await _db.DisposeAsync();
    }

    // ── Test 1: command succeeds and returns parsed metadata ─────────────────

    [Fact]
    public async Task Provision_ReturnsSuccessWithMetadataAndInfo()
    {
        var tenantId = Guid.NewGuid();
        var command = new ProvisionTenantConnectorCommand(tenantId, ConnectorName);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            $"Claude should generate valid metadata for '{ConnectorName}'");

        _createdIds.Add(TenantConnectorId.From(result.Value!.TenantConnectorId));

        result.Value.Metadata.Should().NotBeNullOrWhiteSpace();
        result.Value.Info.Should().NotBeNullOrWhiteSpace();

        // Both must be valid JSON
        var act1 = () => JsonDocument.Parse(result.Value.Metadata);
        var act2 = () => JsonDocument.Parse(result.Value.Info);
        act1.Should().NotThrow("Metadata must be valid JSON");
        act2.Should().NotThrow("Info must be valid JSON");
    }

    // ── Test 2: metadata contains required fields Claude must populate ────────

    [Fact]
    public async Task Provision_MetadataContainsRequiredAuthFields()
    {
        var tenantId = Guid.NewGuid();
        var command = new ProvisionTenantConnectorCommand(tenantId, ConnectorName);

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        _createdIds.Add(TenantConnectorId.From(result.Value!.TenantConnectorId));

        var metadata = JsonDocument.Parse(result.Value.Metadata).RootElement;

        metadata.TryGetProperty("authType", out var authType).Should().BeTrue(
            "metadata must include authType");
        authType.GetString().Should().BeOneOf("APIKey", "OAuth2", "Basic", "Bearer",
            "authType must be one of the known values");

        metadata.TryGetProperty("requiredFields", out var requiredFields).Should().BeTrue(
            "metadata must include requiredFields so we know what to ask the user to fill in");
        requiredFields.ValueKind.Should().Be(JsonValueKind.Array,
            "requiredFields must be a JSON array");
        requiredFields.GetArrayLength().Should().BeGreaterThan(0,
            "at least one credential field must be required");
    }

    // ── Test 3: row is persisted to the database ──────────────────────────────

    [Fact]
    public async Task Provision_SavesRowToDatabase()
    {
        var tenantId = Guid.NewGuid();
        var command = new ProvisionTenantConnectorCommand(tenantId, ConnectorName);

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        var connectorId = TenantConnectorId.From(result.Value!.TenantConnectorId);
        _createdIds.Add(connectorId);

        // Read back from the real DB
        var saved = await _repository.GetByIdAsync(connectorId);

        saved.Should().NotBeNull("the row must exist in the database after provisioning");
        saved!.ConnectorName.Should().Be(ConnectorName);
        saved.TenantId.Value.Should().Be(tenantId);
        saved.Status.Should().Be(TenantConnectorStatus.Pending,
            "connector starts as Pending until credentials are validated");
    }

    // ── Test 4: persisted metadata matches what Claude returned ───────────────

    [Fact]
    public async Task Provision_PersistedMetadataMatchesClaudeOutput()
    {
        var tenantId = Guid.NewGuid();
        var command = new ProvisionTenantConnectorCommand(tenantId, ConnectorName);

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        var connectorId = TenantConnectorId.From(result.Value!.TenantConnectorId);
        _createdIds.Add(connectorId);

        var saved = await _repository.GetByIdAsync(connectorId);
        saved.Should().NotBeNull();

        // Normalise whitespace before comparing — DB round-trip may reformat
        var expectedMetadata = JsonDocument.Parse(result.Value.Metadata).RootElement.GetRawText();
        var actualMetadata   = JsonDocument.Parse(saved!.Metadata).RootElement.GetRawText();
        actualMetadata.Should().Be(expectedMetadata,
            "the metadata stored in DB must be identical to what Claude generated");

        var expectedInfo = JsonDocument.Parse(result.Value.Info).RootElement.GetRawText();
        var actualInfo   = JsonDocument.Parse(saved.Info).RootElement.GetRawText();
        actualInfo.Should().Be(expectedInfo,
            "the info stored in DB must be identical to what Claude generated");
    }

    // ── Test 5: duplicate connector name for the same tenant is rejected ──────

    [Fact]
    public async Task Provision_DuplicateConnectorName_ReturnsConflictError()
    {
        var tenantId = Guid.NewGuid();
        var command = new ProvisionTenantConnectorCommand(tenantId, ConnectorName);

        var first = await _handler.Handle(command, CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        _createdIds.Add(TenantConnectorId.From(first.Value!.TenantConnectorId));

        // Second call with the same tenant + connector name
        var second = await _handler.Handle(command, CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error!.Code.Should().Be("TenantConnector.AlreadyExists");
    }
}
