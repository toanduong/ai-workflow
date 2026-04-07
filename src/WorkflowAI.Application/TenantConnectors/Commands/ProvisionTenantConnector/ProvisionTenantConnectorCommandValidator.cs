using FluentValidation;

namespace WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;

public sealed class ProvisionTenantConnectorCommandValidator : AbstractValidator<ProvisionTenantConnectorCommand>
{
    public ProvisionTenantConnectorCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .Must(id => id != Guid.Empty)
            .WithMessage("TenantId must not be empty.");
        RuleFor(x => x.ConnectorName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(200);
    }
}
