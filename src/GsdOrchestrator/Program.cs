using Anthropic.SDK;
using dotenv.net;
using GsdOrchestrator.Auth;
using GsdOrchestrator.Checkpointing;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows;
using GsdOrchestrator.Workflows.Models;
using GsdOrchestrator.Workflows.States;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Polly;

// ── Load .env before anything else ───────────────────────────────────────────
DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 4));

// ── Simple args parsing ───────────────────────────────────────────────────────
int? issueNumber = null;
string? resumeId = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--issue" && int.TryParse(args[i + 1], out var n)) issueNumber = n;
    if (args[i] == "--resume") resumeId = args[i + 1];
}

if (issueNumber is null && resumeId is null)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run -- --issue <number>");
    Console.Error.WriteLine("  dotnet run -- --resume <workflow-id>");
    Environment.Exit(1);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.AddSimpleConsole(o => o.IncludeScopes = false);
builder.Services.AddLogging(lb => lb.AddFilter("Microsoft", LogLevel.Warning));

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<GitHubPatProvider>();

// ── MCP Client ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMcpClient>(sp =>
{
    var pat = sp.GetRequiredService<GitHubPatProvider>();
    var logger = sp.GetRequiredService<ILogger<McpStdioClient>>();
    return new McpStdioClient(pat.Token, logger);
});
builder.Services.AddSingleton<McpToolDispatcher>();

// ── Polly resilience pipeline ─────────────────────────────────────────────────
builder.Services.AddResiliencePipeline("mcp-tools", pipelineBuilder => pipelineBuilder
    .AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(5),
        ShouldHandle = args =>
            args.Outcome.Exception is McpException { IsTransient: true, IsSecondaryRateLimit: false }
                ? ValueTask.FromResult(true)
                : ValueTask.FromResult(false)
    }));

// ── LLM Client (Anthropic Claude via Anthropic.SDK) ──────────────────────────
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var key = builder.Configuration["ANTHROPIC_API_KEY"]
        ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set in .env");
    var anthropic = new AnthropicClient(new APIAuthentication(key));
    return (IChatClient)anthropic.Messages;
});

// ── Checkpointing ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ICheckpointStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FileCheckpointStore>>();
    var repoRoot = Directory.GetCurrentDirectory();
    return new FileCheckpointStore(repoRoot, logger);
});

// ── Workflow states ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<IWorkflowState, IdleState>();
builder.Services.AddSingleton<IWorkflowState, AnalyzingState>();
builder.Services.AddSingleton<IWorkflowState, BranchingState>();
builder.Services.AddSingleton<IWorkflowState, EditingState>();
builder.Services.AddSingleton<IWorkflowState, ValidatingState>();
builder.Services.AddSingleton<IWorkflowState, CommittingState>();
builder.Services.AddSingleton<IWorkflowState, PrCreatingState>();
builder.Services.AddSingleton<IWorkflowState, ReviewingState>();
builder.Services.AddSingleton<IWorkflowState, DocumentingState>();

builder.Services.AddSingleton<GsdStateMachine>();

var host = builder.Build();

// ── Run ───────────────────────────────────────────────────────────────────────
var sm = host.Services.GetRequiredService<GsdStateMachine>();
var mcp = host.Services.GetRequiredService<IMcpClient>();
var config = host.Services.GetRequiredService<IConfiguration>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

// Initialize MCP server process
await mcp.InitializeAsync();
logger.LogInformation("GitHub MCP Server ready");

GsdWorkflowContext result;

if (resumeId is not null)
{
    result = await sm.ResumeAsync(resumeId, CancellationToken.None);
}
else
{
    var owner = config["GSD_GITHUB_OWNER"]
        ?? throw new InvalidOperationException("GSD_GITHUB_OWNER not set");
    var repo = config["GSD_GITHUB_REPO"]
        ?? throw new InvalidOperationException("GSD_GITHUB_REPO not set");

    result = await sm.RunAsync(owner, repo, issueNumber!.Value, CancellationToken.None);
}

await (mcp as IAsyncDisposable)!.DisposeAsync();

if (result.CurrentState == WorkflowState.Done)
{
    Console.WriteLine();
    Console.WriteLine($"✓ PR created:   {result.PullRequest?.PrUrl}");
    Console.WriteLine($"✓ Docs updated: docs/github-mcp-tools.md, CHANGELOG.md");
    Console.WriteLine($"  Workflow ID:  {result.WorkflowId}");
    Environment.Exit(0);
}
else
{
    Console.Error.WriteLine($"✗ Workflow failed: {result.FailureReason}");
    Console.Error.WriteLine($"  Resume with: dotnet run -- --resume {result.WorkflowId}");
    Environment.Exit(1);
}
