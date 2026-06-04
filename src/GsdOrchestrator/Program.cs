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
using Polly.CircuitBreaker;
using Serilog;
using Serilog.Formatting.Compact;

// ── Load .env before anything else ─────────────────────────────────────────────
DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 4));

// ── Simple args parsing ──────────────────────────────────────────────────
int? issueNumber = null;
string? resumeId = null;
bool watchMode = false;
bool triageModeOnly = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--issue" && i + 1 < args.Length && int.TryParse(args[i + 1], out var n)) issueNumber = n;
    if (args[i] == "--resume" && i + 1 < args.Length) resumeId = args[i + 1];
    if (args[i] == "--watch") watchMode = true;
    if (args[i] == "--triage") triageModeOnly = true;
}

if (triageModeOnly && issueNumber is null)
{
    Console.Error.WriteLine("Error: --triage requires --issue <number>");
    Environment.Exit(1);
}

if (issueNumber is null && resumeId is null && !watchMode)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run -- --issue <number>               Run workflow for a specific issue");
    Console.Error.WriteLine("  dotnet run -- --issue <number> --triage      Classify issue only (no code changes)");
    Console.Error.WriteLine("  dotnet run -- --resume <workflow-id>         Resume an interrupted workflow");
    Console.Error.WriteLine("  dotnet run -- --watch                        Poll open issues and process them automatically");
    Environment.Exit(1);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// ── Logging ──────────────────────────────────────────────────────────────
builder.Services.AddSerilog(lc =>
    lc.MinimumLevel.Information()
      .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
      .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
      .Enrich.FromLogContext()
      .WriteTo.Console(new CompactJsonFormatter()));

// ── Auth ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<GitHubPatProvider>();

// ── MCP Client ──────────────────────────────────────────────────────────────────
var binaryPath = builder.Configuration["GSD_MCP_BINARY"] ?? FindMcpBinary();

builder.Services.AddSingleton<IMcpClient>(sp =>
{
    var pat = sp.GetRequiredService<GitHubPatProvider>();
    var mcpLogger = sp.GetRequiredService<ILogger<McpStdioClient>>();
    return new McpStdioClient(pat.Token, binaryPath, mcpLogger);
});
builder.Services.AddSingleton<McpToolDispatcher>();

// ── Polly resilience pipeline ────────────────────────────────────────────────────────────
// AddCircuitBreaker MUST come before AddRetry — outermost strategy trips first,
// preventing retry budget waste when MCP is unavailable (D-07, D-08).
builder.Services.AddResiliencePipeline("mcp-tools", pipelineBuilder => pipelineBuilder
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        // "5 consecutive failures within 60s" expressed as ratio-based (Polly v8 only has ratio-based CB)
        FailureRatio = 1.0,
        SamplingDuration = TimeSpan.FromSeconds(60),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30),
        ShouldHandle = new PredicateBuilder()
            .Handle<McpException>(ex => ex.IsTransient && !ex.IsSecondaryRateLimit)
    })
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

// ── LLM Client (Anthropic Claude via Anthropic.SDK) ────────────────────────────
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var key = builder.Configuration["ANTHROPIC_API_KEY"]
        ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set in .env");
    var anthropic = new AnthropicClient(new APIAuthentication(key));
    return (IChatClient)anthropic.Messages;
});

// ── Checkpointing ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ICheckpointStore>(sp =>
{
    var cpLogger = sp.GetRequiredService<ILogger<FileCheckpointStore>>();
    var repoRoot = Directory.GetCurrentDirectory();
    return new FileCheckpointStore(repoRoot, cpLogger);
});

// ── Workflow states ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IWorkflowState, IdleState>();
builder.Services.AddSingleton<IWorkflowState, TriagingState>();
builder.Services.AddSingleton<IWorkflowState, AnalyzingState>();
builder.Services.AddSingleton<IWorkflowState, BranchingState>();
builder.Services.AddSingleton<IWorkflowState, EditingState>();
builder.Services.AddSingleton<IWorkflowState, TestGeneratingState>();
builder.Services.AddSingleton<IWorkflowState, ValidatingState>();
builder.Services.AddSingleton<IWorkflowState, CommittingState>();
builder.Services.AddSingleton<IWorkflowState, PrCreatingState>();
builder.Services.AddSingleton<IWorkflowState, ReviewingState>();
builder.Services.AddSingleton<IWorkflowState, DocumentingState>();

builder.Services.AddSingleton<GsdStateMachine>();

var host = builder.Build();

// ── Run ───────────────────────────────────────────────────────────────────────────
var sm = host.Services.GetRequiredService<GsdStateMachine>();
var mcp = host.Services.GetRequiredService<IMcpClient>();
var config = host.Services.GetRequiredService<IConfiguration>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

// Initialize MCP server process
await mcp.InitializeAsync();
logger.LogInformation("GitHub MCP Server ready (binary: {Binary})", binaryPath);

var owner = config["GSD_GITHUB_OWNER"]
    ?? throw new InvalidOperationException("GSD_GITHUB_OWNER not set");
var repo = config["GSD_GITHUB_REPO"]
    ?? throw new InvalidOperationException("GSD_GITHUB_REPO not set");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var mcpDispatcher = host.Services.GetRequiredService<McpToolDispatcher>();

if (watchMode)
{
    await RunWatchModeAsync(sm, mcpDispatcher, owner, repo, logger, cts.Token);
}
else if (resumeId is not null)
{
    var result = await sm.ResumeAsync(resumeId, cts.Token);
    PrintResult(result);
}
else
{
    var result = await sm.RunAsync(owner, repo, issueNumber!.Value, triageModeOnly, cts.Token);
    PrintResult(result);
}

await (mcp as IAsyncDisposable)!.DisposeAsync();

// ── Watch mode ──────────────────────────────────────────────────────────────────
static async Task RunWatchModeAsync(
    GsdStateMachine sm, McpToolDispatcher mcpDispatcher,
    string owner, string repo,
    ILogger<Program> logger, CancellationToken ct)
{
    var pollInterval = TimeSpan.FromMinutes(5);
    // Bounded set: keep only the last 500 processed issue numbers to avoid unbounded growth.
    // When full, the oldest 100 entries are evicted so re-opened issues can be reprocessed.
    const int processedIssuesCapacity = 500;
    const int processedIssuesEvictCount = 100;
    var processedIssues = new HashSet<int>();
    logger.LogInformation("Watch mode started — polling {Owner}/{Repo} every {Interval}m", owner, repo, pollInterval.TotalMinutes);

    while (!ct.IsCancellationRequested)
    {
        try
        {
            // List open issues via MCP
            var result = await mcpDispatcher.CallAsync("list_issues", new System.Text.Json.Nodes.JsonObject
            {
                ["owner"] = owner,
                ["repo"] = repo,
                ["state"] = "open",
                ["perPage"] = 20
            }, ct);

            var issues = result.ParseInnerJson()?.AsArray() ?? [];
            var openNumbers = issues
                .Select(i => i?["number"]?.GetValue<int>())
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();

            var pending = openNumbers.Except(processedIssues).ToList();
            logger.LogInformation("Watch: {Open} open issues, {Pending} unprocessed", openNumbers.Count, pending.Count);

            foreach (var num in pending)
            {
                if (ct.IsCancellationRequested) break;
                logger.LogInformation("Processing issue #{Number}", num);
                // triageModeOnly: false — watch mode always runs the full workflow
                var ctx = await sm.RunAsync(owner, repo, num, triageModeOnly: false, ct);
                if (processedIssues.Count >= processedIssuesCapacity)
                {
                    // Evict oldest entries to keep set bounded
                    foreach (var old in processedIssues.Take(processedIssuesEvictCount).ToList())
                        processedIssues.Remove(old);
                }
                processedIssues.Add(num);
                PrintResult(ctx);
            }
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Watch loop error — retrying in {Interval}m", pollInterval.TotalMinutes);
        }

        try { await Task.Delay(pollInterval, ct); }
        catch (OperationCanceledException) { break; }
    }

    logger.LogInformation("Watch mode stopped");
}

static void PrintResult(GsdWorkflowContext result)
{
    if (result.CurrentState == WorkflowState.Done)
    {
        Console.WriteLine();
        if (result.Triage is not null && result.PullRequest is null)
        {
            // Triage-only run — no PR was created
            Console.WriteLine($"Triage complete: [{result.Triage.Classification}] {result.Triage.Reason}");
        }
        else
        {
            Console.WriteLine($"✓ PR created:   {result.PullRequest?.PrUrl}");
            Console.WriteLine($"✓ Docs updated: docs/github-mcp-tools.md, CHANGELOG.md");
        }
        Console.WriteLine($"  Workflow ID:  {result.WorkflowId}");
    }
    else
    {
        Console.Error.WriteLine($"✗ Workflow failed: {result.FailureReason}");
        Console.Error.WriteLine($"  Resume with: dotnet run -- --resume {result.WorkflowId}");
    }
}

// ── Binary discovery ──────────────────────────────────────────────────────────────
static string FindMcpBinary()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "github-mcp-server.exe");
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    // Fallback: hope it's on PATH
    return "github-mcp-server.exe";
}
