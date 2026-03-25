using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Functions.Extensions;

public static class HttpRequestExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T?> ReadFromJsonAsync<T>(this HttpRequestData request)
    {
        return await JsonSerializer.DeserializeAsync<T>(request.Body, JsonOptions);
    }

    public static async Task<HttpResponseData> CreateResultResponseAsync<T>(
        this HttpRequestData request,
        Result<T> result,
        HttpStatusCode successStatusCode = HttpStatusCode.OK)
    {
        if (result.IsSuccess)
        {
            var response = request.CreateResponse(successStatusCode);
            await response.WriteAsJsonAsync(result.Value);
            return response;
        }

        return await CreateErrorResponseAsync(request, result.Error!);
    }

    public static async Task<HttpResponseData> CreateResultResponseAsync(
        this HttpRequestData request,
        Result result)
    {
        if (result.IsSuccess)
        {
            return request.CreateResponse(HttpStatusCode.NoContent);
        }

        return await CreateErrorResponseAsync(request, result.Error!);
    }

    private static async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData request, Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => HttpStatusCode.BadRequest,
            ErrorType.NotFound => HttpStatusCode.NotFound,
            ErrorType.Conflict => HttpStatusCode.Conflict,
            ErrorType.Unauthorized => HttpStatusCode.Forbidden,
            _ => HttpStatusCode.InternalServerError
        };

        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error.Code, error.Message });
        return response;
    }
}
