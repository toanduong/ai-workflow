# Anthropic C# SDK — IChatClient Integration Plan

## Overview

Add Anthropic Claude as a second AI provider alongside the existing Azure OpenAI integration,
using `Microsoft.Extensions.AI`'s `IChatClient` abstraction for provider-agnostic injection.

**Target framework:** .NET 10.0  
**SDK:** `Anthropic.SDK` (community C# SDK by tmarois)  
**Abstraction:** `Microsoft.Extensions.AI.IChatClient`

---

## Architecture

```
Application Layer
└── IAnthropicService          (new interface, mirrors IAzureOpenAIService)
    └── reuses AICompletionResult + AIToolDefinition records (already defined)

Infrastructure Layer
├── AnthropicOptions            (new — config binding)
├── AnthropicService            (new — implements IAnthropicService via IChatClient)
└── DependencyInjection.cs      (updated — registers IChatClient + IAnthropicService)

Package Management
├── Directory.Packages.props    (updated — adds 3 new packages)
└── WorkflowAI.Infrastructure.csproj (updated — adds 2 package references)
```

---

## Step-by-Step Implementation

### Step 1 — Add NuGet Packages

**File:** `Directory.Packages.props`

Add under the AI section:

```xml
<!-- Anthropic Claude SDK -->
<PackageVersion Include="Anthropic.SDK" Version="0.12.0" />
<PackageVersion Include="Microsoft.Extensions.AI" Version="9.5.0" />
<PackageVersion Include="Microsoft.Extensions.AI.Abstractions" Version="9.5.0" />
```

> **Note:** Verify latest stable versions on NuGet before applying:
> - https://www.nuget.org/packages/Anthropic.SDK
> - https://www.nuget.org/packages/Microsoft.Extensions.AI

**File:** `src/WorkflowAI.Infrastructure/WorkflowAI.Infrastructure.csproj`

Add inside `<ItemGroup>`:

```xml
<PackageReference Include="Anthropic.SDK" />
<PackageReference Include="Microsoft.Extensions.AI" />
```

---

### Step 2 — Create `AnthropicOptions`

**File:** `src/WorkflowAI.Infrastructure/AI/AnthropicOptions.cs`

```csharp
namespace WorkflowAI.Infrastructure.AI;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "claude-sonnet-4-5";
}
```

Bind from `appsettings.json`:

```json
"Anthropic": {
  "ApiKey": "<your-api-key>",
  "DefaultModel": "claude-sonnet-4-5"
}
```

---

### Step 3 — Create `IAnthropicService` (Application Layer)

**File:** `src/WorkflowAI.Application/Common/Interfaces/IAnthropicService.cs`

```csharp
namespace WorkflowAI.Application.Common.Interfaces;

// Reuses AICompletionResult and AIToolDefinition defined in IAzureOpenAIService.cs

public interface IAnthropicService
{
    Task<AICompletionResult> CompleteAsync(
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default);

    Task<AICompletionResult> CompleteWithToolsAsync(
        string prompt,
        IReadOnlyList<AIToolDefinition> tools,
        string? model = null,
        CancellationToken cancellationToken = default);
}
```

> `model` is nullable — falls back to `AnthropicOptions.DefaultModel` when null.  
> Records `AICompletionResult` and `AIToolDefinition` already exist in `IAzureOpenAIService.cs` — no duplication needed.

---

### Step 4 — Create `AnthropicService` (Infrastructure Layer)

**File:** `src/WorkflowAI.Infrastructure/AI/AnthropicService.cs`

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.AI;

public sealed class AnthropicService(
    IChatClient chatClient,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicService> logger) : IAnthropicService
{
    public async Task<AICompletionResult> CompleteAsync(
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chatOptions = model is not null
                ? new ChatOptions { ModelId = model }
                : null;

            var response = await chatClient.CompleteAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                chatOptions,
                cancellationToken);

            var content = response.Message.Text ?? string.Empty;
            var tokens = response.Usage?.TotalTokenCount ?? 0;

            return new AICompletionResult(content, tokens, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Anthropic completion failed for model {Model}",
                model ?? options.Value.DefaultModel);
            return new AICompletionResult(string.Empty, 0, false, ex.Message);
        }
    }

    public async Task<AICompletionResult> CompleteWithToolsAsync(
        string prompt,
        IReadOnlyList<AIToolDefinition> tools,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var aiTools = tools
                .Select(t => AIFunctionFactory.Create(() => { }, t.Name, t.Description))
                .Cast<AITool>()
                .ToList();

            var chatOptions = new ChatOptions
            {
                ModelId = model,
                Tools = aiTools
            };

            var response = await chatClient.CompleteAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                chatOptions,
                cancellationToken);

            var content = response.Message.Text ?? string.Empty;
            var tokens = response.Usage?.TotalTokenCount ?? 0;

            return new AICompletionResult(content, tokens, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Anthropic tool completion failed for model {Model}",
                model ?? options.Value.DefaultModel);
            return new AICompletionResult(string.Empty, 0, false, ex.Message);
        }
    }
}
```

> **Note on tool calling:** `AIFunctionFactory.Create` in `Microsoft.Extensions.AI` wraps a .NET
> delegate. `AIToolDefinition.ParametersJson` (raw JSON schema) cannot be passed directly.
> See the **Design Decisions** section below for alternatives.

---

### Step 5 — Register in DI

**File:** `src/WorkflowAI.Infrastructure/DependencyInjection.cs`

Add after the Azure OpenAI block:

```csharp
// Anthropic Claude
services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
services.AddSingleton<IChatClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
    return new AnthropicClient(opts.ApiKey)
        .Messages
        .AsChatClient(opts.DefaultModel);
});
services.AddScoped<IAnthropicService, AnthropicService>();
```

Required usings:
```csharp
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using WorkflowAI.Infrastructure.AI;
```

---

## Design Decisions

### Why `IChatClient` as the DI registration?

`IChatClient` from `Microsoft.Extensions.AI` is provider-agnostic. Registering it in DI means:
- `AnthropicService` stays decoupled from `Anthropic.SDK` internals
- Future provider swap (e.g., local Ollama, Azure AI Inference) only requires re-wiring DI
- Middleware (logging, caching, retries) can be layered via `.Use*(...)` builder extensions

### Why keep `IAnthropicService` instead of injecting `IChatClient` everywhere?

- Mirrors the existing `IAzureOpenAIService` pattern — consistent with project conventions
- Domain/Application layer stays free of `Microsoft.Extensions.AI` assembly references
- Reuses `AICompletionResult` and `AIToolDefinition` — no duplication with OpenAI path

### Tool calling limitation

`AIToolDefinition.ParametersJson` holds a raw JSON schema string (matching the OpenAI pattern).  
`Microsoft.Extensions.AI` expects `AITool` objects wrapping real .NET delegates — there is no  
direct "raw schema → AITool" conversion built in.

**Options:**
1. **Keep raw schema approach** — call `Anthropic.SDK` directly (bypass `IChatClient` for tools)
2. **Refactor callers** — pass `AIFunction` delegates directly from the call site
3. **Custom AIFunction subclass** — wrap the JSON schema in an `AIFunction` implementation

Option 1 is the lowest-risk change. Option 3 is the cleanest long-term design.

---

## Configuration Reference

### `appsettings.json` / `local.settings.json`

```json
{
  "Anthropic": {
    "ApiKey": "<anthropic-api-key>",
    "DefaultModel": "claude-sonnet-4-5"
  }
}
```

### Available Claude model IDs (as of mid-2025)

| Model | ID |
|---|---|
| Claude Sonnet 4.5 | `claude-sonnet-4-5` |
| Claude Sonnet 4.6 | `claude-sonnet-4-6` |
| Claude Haiku 4.5 | `claude-haiku-4-5-20251001` |
| Claude Opus 4.6 | `claude-opus-4-6` |

---

## Files to Create / Modify

| Action | File |
|--------|------|
| Modify | `Directory.Packages.props` |
| Modify | `src/WorkflowAI.Infrastructure/WorkflowAI.Infrastructure.csproj` |
| **Create** | `src/WorkflowAI.Infrastructure/AI/AnthropicOptions.cs` |
| **Create** | `src/WorkflowAI.Application/Common/Interfaces/IAnthropicService.cs` |
| **Create** | `src/WorkflowAI.Infrastructure/AI/AnthropicService.cs` |
| Modify | `src/WorkflowAI.Infrastructure/DependencyInjection.cs` |

---

## Testing Checklist

- [ ] Unit test `AnthropicService.CompleteAsync` with a mocked `IChatClient`
- [ ] Verify `AnthropicOptions` binds correctly from config
- [ ] Confirm `IChatClient` resolves from DI without errors on startup
- [ ] Integration test against Anthropic API with a real key (use test environment)
- [ ] Confirm `TreatWarningsAsErrors` passes (no nullable violations)
