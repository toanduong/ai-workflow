using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Services;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
