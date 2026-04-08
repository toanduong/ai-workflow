using FluentValidation;

namespace WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;

public sealed class ProvisionTenantConnectorCommandValidator : AbstractValidator<ProvisionTenantConnectorCommand>
{
    public ProvisionTenantConnectorCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ConnectorType).NotEmpty().MaximumLength(200);
    }
}
