using FluentValidation;

namespace WorkflowAI.Application.Connectors.Commands.CreateConnector;

public sealed class CreateConnectorCommandValidator : AbstractValidator<CreateConnectorCommand>
{
    public CreateConnectorCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ConnectorType).NotEmpty();
        RuleFor(x => x.AuthModel).NotEmpty();
    }
}
