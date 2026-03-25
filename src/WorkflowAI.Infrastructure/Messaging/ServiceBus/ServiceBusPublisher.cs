using System.Text.Json;
using Azure.Messaging.ServiceBus;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Messaging.ServiceBus;

public sealed class ServiceBusPublisher(ServiceBusClient client) : IServiceBusPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default)
        where T : class
    {
        await PublishAsync(topicName, message, Guid.NewGuid().ToString(), cancellationToken);
    }

    public async Task PublishAsync<T>(string topicName, T message, string correlationId, CancellationToken cancellationToken = default)
        where T : class
    {
        await using var sender = client.CreateSender(topicName);

        var body = JsonSerializer.Serialize(message, JsonOptions);
        var serviceBusMessage = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            CorrelationId = correlationId,
            Subject = typeof(T).Name,
            MessageId = Guid.NewGuid().ToString()
        };

        await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}
