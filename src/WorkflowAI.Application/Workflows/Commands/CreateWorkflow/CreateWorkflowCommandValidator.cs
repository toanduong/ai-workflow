using FluentValidation;

namespace WorkflowAI.Application.Workflows.Commands.CreateWorkflow;

public sealed class CreateWorkflowCommandValidator : AbstractValidator<CreateWorkflowCommand>
{
    public CreateWorkflowCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workflow name is required.")
            .MaximumLength(200).WithMessage("Workflow name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.Name)
                .NotEmpty().WithMessage("Step name is required.");

            step.RuleFor(s => s.StepType)
                .NotEmpty().WithMessage("Step type is required.");

            step.RuleFor(s => s.TimeoutMinutes)
                .GreaterThan(0).WithMessage("Timeout must be greater than 0.");
        });
    }
}
