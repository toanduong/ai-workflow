using System.Net;
using FluentAssertions;
using NSubstitute;
using WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.TenantConnectors;

public class ValidateTenantConnectorCommandHandlerTests
{
    private readonly ITenantConnectorRepository _repository = Substitute.For<ITenantConnectorRepository>();

    private ValidateTenantConnectorCommandHandler CreateHandler(HttpClient httpClient) =>
        new(_repository, httpClient);

    // ── APIKey auth ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidApolloApiKey_ActivatesConnector()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var command = new ValidateTenantConnectorCommand(
            connector.Id.Value,
            new Dictionary<string, string> { ["api_key"] = "valid-apollo-api-key" });

        var result = await CreateHandler(httpClient).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        connector.Status.Should().Be(TenantConnectorStatus.Active);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<TenantConnector>(c => c.Status == TenantConnectorStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidApiKey_MarksConnectorFailed()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);
        var command = new ValidateTenantConnectorCommand(
            connector.Id.Value,
            new Dictionary<string, string> { ["api_key"] = "invalid-key" });

        var result = await CreateHandler(httpClient).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        connector.Status.Should().Be(TenantConnectorStatus.Failed);
    }

    // ── Bearer / OAuth2 auth ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_BearerAuth_AppliesAuthorizationHeader()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Salesforce")
            .WithMetadata(
                """{"authType":"Bearer","requiredFields":["token"],"testEndpoint":{"method":"GET","path":"https://salesforce.example.com/services/data/v57.0"}}""",
                """{"description":"Salesforce CRM","docsUrl":"","capabilities":[],"rateLimits":"","webhookSupport":false}""")
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        HttpRequestMessage? captured = null;
        var httpClient = CreateCapturingHttpClient(HttpStatusCode.OK, req => captured = req);

        var command = new ValidateTenantConnectorCommand(
            connector.Id.Value,
            new Dictionary<string, string> { ["token"] = "my-bearer-token" });

        await CreateHandler(httpClient).Handle(command, CancellationToken.None);

        captured!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("my-bearer-token");
    }

    // ── Basic auth ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_BasicAuth_AppliesBase64Credentials()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Jira")
            .WithMetadata(
                """{"authType":"Basic","requiredFields":["username","password"],"testEndpoint":{"method":"GET","path":"https://jira.example.com/rest/api/2/myself"}}""",
                """{"description":"Jira project management","docsUrl":"","capabilities":[],"rateLimits":"","webhookSupport":false}""")
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        HttpRequestMessage? captured = null;
        var httpClient = CreateCapturingHttpClient(HttpStatusCode.OK, req => captured = req);

        var command = new ValidateTenantConnectorCommand(
            connector.Id.Value,
            new Dictionary<string, string> { ["username"] = "user", ["password"] = "pass" });

        await CreateHandler(httpClient).Handle(command, CancellationToken.None);

        captured!.Headers.Authorization!.Scheme.Should().Be("Basic");
        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(captured.Headers.Authorization.Parameter!));
        decoded.Should().Be("user:pass");
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ConnectorNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        var command = new ValidateTenantConnectorCommand(
            Guid.NewGuid(),
            new Dictionary<string, string> { ["api_key"] = "any" });

        var result = await CreateHandler(CreateMockHttpClient(HttpStatusCode.OK))
            .Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotFound");
    }

    [Fact]
    public async Task Handle_MetadataHasNoTestEndpoint_MarksConnectorFailed()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .WithMetadata(
                """{"authType":"APIKey","requiredFields":["api_key"],"endpoints":{}}""",
                """{"description":"Apollo","docsUrl":"","capabilities":[],"rateLimits":"","webhookSupport":false}""")
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        var command = new ValidateTenantConnectorCommand(
            connector.Id.Value,
            new Dictionary<string, string> { ["api_key"] = "some-key" });

        var result = await CreateHandler(CreateMockHttpClient(HttpStatusCode.OK))
            .Handle(command, CancellationToken.None);

        result.Value.Should().BeFalse();
        connector.Status.Should().Be(TenantConnectorStatus.Failed);
        connector.FailureReason.Should().Contain("testEndpoint");
    }

    [Fact]
    public async Task Handle_NetworkFailure_MarksConnectorFailed()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        var command = new ValidateTenantConnectorCommand(
            connector.Id.Value,
            new Dictionary<string, string> { ["api_key"] = "any" });

        var result = await CreateHandler(
            CreateThrowingHttpClient(new HttpRequestException("Network unreachable")))
            .Handle(command, CancellationToken.None);

        result.Value.Should().BeFalse();
        connector.Status.Should().Be(TenantConnectorStatus.Failed);
        connector.FailureReason.Should().Be("Network unreachable");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode) =>
        new(new StubHttpMessageHandler(statusCode));

    private static HttpClient CreateCapturingHttpClient(
        HttpStatusCode statusCode, Action<HttpRequestMessage> capture) =>
        new(new CapturingHttpMessageHandler(statusCode, capture));

    private static HttpClient CreateThrowingHttpClient(Exception ex) =>
        new(new ThrowingHttpMessageHandler(ex));
}

internal sealed class StubHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(statusCode));
}

internal sealed class CapturingHttpMessageHandler(
    HttpStatusCode statusCode, Action<HttpRequestMessage> capture) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        capture(request);
        return Task.FromResult(new HttpResponseMessage(statusCode));
    }
}

internal sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => throw exception;
}
