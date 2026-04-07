using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.AIAgent;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.AIAgent.Commands.ExecuteAIStep;

public sealed class ExecuteAIStepCommandHandler(
    IAIAgentTaskRepository taskRepository,
    IClaudeAIService claudeService)
    : IRequestHandler<ExecuteAIStepCommand, Result>
{
    public async Task<Result> Handle(ExecuteAIStepCommand request, CancellationToken ct)
    {
        var prompt = request.Configuration ?? "";
        var task = AIAgentTask.Create(request.StepExecutionId, prompt, null, "claude-opus-4-6");
        task.StartProcessing();
        await taskRepository.AddAsync(task, ct);

        var response = await claudeService.CompleteAsync(prompt, ct);

        if (response.Success)
            task.Complete(response.Content, response.TokensUsed);
        else
            task.Fail();

        await taskRepository.UpdateAsync(task, ct);
        return Result.Success();
    }
}
