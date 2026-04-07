using FluentValidation;

namespace WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;

public sealed class ValidateTenantConnectorCommandValidator
    : AbstractValidator<ValidateTenantConnectorCommand>
{
    public ValidateTenantConnectorCommandValidator()
    {
        RuleFor(x => x.TenantConnectorId)
            .NotEmpty();

        RuleFor(x => x.Credentials)
            .NotNull()
            .Must(c => c.Count > 0)
                .WithMessage("At least one credential field is required.");

        RuleForEach(x => x.Credentials)
            .Must(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .WithMessage("Credential keys and values must not be empty.");
    }
}
