using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Real integration tests that hit the live Apollo.io API.
/// Requires a valid Apollo API key set in APOLLO_API_KEY env var or hardcoded for dev.
/// </summary>
public class ApolloConnectionTests
{
    private const string ApolloApiKey = "2KnDUjWTvMGew6imUVTACw";
    private const string ApolloBaseUrl = "https://api.apollo.io/v1";

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        return client;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: Valid API key returns 200 on health/auth check
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ApolloApiKey_IsValid_WhenHealthCheckShowsLoggedIn()
    {
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApolloBaseUrl}/auth/health");
        request.Headers.Add("X-Api-Key", ApolloApiKey);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("is_logged_in").GetBoolean().Should().BeTrue(
            "a valid Apollo API key should result in is_logged_in=true");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: Invalid API key — health endpoint returns 200 but is_logged_in=false
    //         Apollo's health check always returns 200; auth state is in the body.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ApolloApiKey_IsInvalid_WhenHealthCheckShowsNotLoggedIn()
    {
        using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApolloBaseUrl}/auth/health");
        request.Headers.Add("X-Api-Key", "invalid-key-that-does-not-exist");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        // Apollo health endpoint always returns 200, but is_logged_in reflects key validity
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("is_logged_in").GetBoolean().Should().BeFalse(
            "an invalid API key should result in is_logged_in=false");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: People search requires a paid plan — free tier returns 403 (plan restriction)
    //         The key is valid (health check passes), but this endpoint is plan-gated.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Apollo_PeopleSearch_Returns403_OnFreeTierPlanRestriction()
    {
        using var client = CreateClient();

        var payload = JsonSerializer.Serialize(new
        {
            api_key = ApolloApiKey,
            q_organization_domains = "apollo.io",
            page = 1,
            per_page = 1
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{ApolloBaseUrl}/mixed_people/search")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", ApolloApiKey);

        var response = await client.SendAsync(request);

        // Free tier: 403 is expected here — plan restriction, not auth failure.
        // The key itself IS valid (verified by health check test above).
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "mixed_people/search requires a paid Apollo plan — free tier returns 403");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: ValidateTenantConnector flow — simulate what the command handler does
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ValidateTenantConnectorFlow_WithApolloMetadata_ActivatesSuccessfully()
    {
        // This simulates exactly what ValidateTenantConnectorCommandHandler does:
        // 1. Extract testEndpoint from metadata
        // 2. Call it with the API key
        // 3. Expect success

        var metadata = """
            {
              "authType": "APIKey",
              "requiredFields": ["api_key"],
              "testEndpoint": {"method": "GET", "path": "https://api.apollo.io/v1/auth/health"},
              "endpoints": {},
              "configSchema": {}
            }
            """;

        var doc = JsonDocument.Parse(metadata);
        var testEndpoint = doc.RootElement.GetProperty("testEndpoint");
        var method = testEndpoint.GetProperty("method").GetString()!;
        var path = testEndpoint.GetProperty("path").GetString()!;

        using var client = CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-Api-Key", ApolloApiKey);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Apollo testEndpoint {method} {path} should return 200");
        json.GetProperty("is_logged_in").GetBoolean().Should().BeTrue(
            "the API key should be recognised as valid by Apollo");
    }
}
