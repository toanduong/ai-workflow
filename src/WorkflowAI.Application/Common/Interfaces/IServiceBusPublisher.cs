namespace WorkflowAI.Application.Common.Interfaces;

public interface IServiceBusPublisher
{
    Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default) where T : class;
    Task PublishAsync<T>(string topicName, T message, string correlationId, CancellationToken cancellationToken = default) where T : class;
}
