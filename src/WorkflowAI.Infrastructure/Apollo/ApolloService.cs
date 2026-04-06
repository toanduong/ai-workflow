using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Apollo;

public sealed class ApolloService(
    HttpClient httpClient,
    IOptions<ApolloOptions> options,
    ILogger<ApolloService> logger) : IApolloService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private void SetAuthHeader()
    {
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", options.Value.ApiKey);
    }

    public async Task<ApolloContactResult?> GetContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthHeader();
            var response = await httpClient.GetAsync(
                $"{options.Value.BaseUrl}/contacts/{contactId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Apollo GetContact failed for {ContactId}: {Status}", contactId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("contact", out var contact))
                return null;

            return new ApolloContactResult(
                Id: contact.TryGetProperty("id", out var id) ? id.GetString() ?? contactId : contactId,
                FirstName: contact.TryGetProperty("first_name", out var fn) ? fn.GetString() : null,
                LastName: contact.TryGetProperty("last_name", out var ln) ? ln.GetString() : null,
                Email: contact.TryGetProperty("email", out var em) ? em.GetString() : null,
                Title: contact.TryGetProperty("title", out var ti) ? ti.GetString() : null,
                Organization: contact.TryGetProperty("organization_name", out var org) ? org.GetString() : null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching Apollo contact {ContactId}", contactId);
            return null;
        }
    }

    public async Task<bool> UpdateContactAsync(string contactId, Dictionary<string, object> fields, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthHeader();
            var body = JsonSerializer.Serialize(new { contact = fields }, JsonOptions);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await httpClient.PatchAsync(
                $"{options.Value.BaseUrl}/contacts/{contactId}", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Apollo UpdateContact failed for {ContactId}: {Status}", contactId, response.StatusCode);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating Apollo contact {ContactId}", contactId);
            return false;
        }
    }

    public async Task<bool> AddToSequenceAsync(string contactId, string sequenceId, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthHeader();
            var body = JsonSerializer.Serialize(new
            {
                sequence_id = sequenceId,
                contact_ids = new[] { contactId }
            }, JsonOptions);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"{options.Value.BaseUrl}/sequences/{sequenceId}/add_contact_ids", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Apollo AddToSequence failed for contact {ContactId}: {Status}", contactId, response.StatusCode);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding contact {ContactId} to sequence {SequenceId}", contactId, sequenceId);
            return false;
        }
    }
}
