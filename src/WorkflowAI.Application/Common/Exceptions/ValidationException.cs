using FluentValidation.Results;

namespace WorkflowAI.Application.Common.Exceptions;

public sealed class ValidationException : Exception
{
    public IReadOnlyList<ValidationFailure> Errors { get; }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures occurred.")
    {
        Errors = failures.ToList().AsReadOnly();
    }
}
