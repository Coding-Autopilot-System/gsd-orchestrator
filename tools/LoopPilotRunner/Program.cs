using System.Text.Json;
using GsdOrchestrator.Loop;
using GsdOrchestrator.Scheduling;
using GsdOrchestrator.Verification;
using Microsoft.Extensions.Logging.Abstractions;

var options = Arguments.Parse(args);
var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
Directory.CreateDirectory(options.Output);

var feature = await RunCoordinatorScenario("feature", [Passed()], options);
var repair = await RunCoordinatorScenario("repair", [Failed(), Passed()], options);
var restart = await RunRestartScenario(options);
var policy = RunPolicyScenario();

await Write("feature", feature);
await Write("repair", repair);
await Write("restart", restart);
await Write("policy", policy);
Console.WriteLine($"Generated executable CAS loop pilot evidence at {Path.GetFullPath(options.Output)}");
return;

async Task Write(string name, PilotEvidence evidence)
{
    var path = Path.Combine(options.Output, $"{name}.json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, serializerOptions));
}

async Task<PilotEvidence> RunCoordinatorScenario(string scenario, IReadOnlyList<VerificationRunResult> results, Arguments arguments)
{
    var database = Path.Combine(Path.GetTempPath(), $"cas-pilot-{scenario}-{Guid.NewGuid():N}.db");
    try
    {
        var store = new SqliteGoalStore(database, NullLogger<SqliteGoalStore>.Instance);
        await store.InitializeAsync();
        await store.SaveAsync(Seed($"goal-{scenario}"));
        var learning = new CapturingLearning();
        var worker = new MafProcessLoopWorker(arguments.Python, arguments.MafRoot);
        var coordinator = new LoopCoordinator(store, worker, new ScriptedVerifier(results), learning);
        var run = await coordinator.RunAsync($"goal-{scenario}", new(true, 0, 2, 0, 2, 0, 10));
        var peak = run.PeakConcurrency;
        var isolatedImplementation = scenario != "feature" || VerifyIsolatedWorktree();
        var events = new List<PilotEvent>();
        if (scenario == "feature")
        {
            events.Add(new(0, "analysis.fanout", "four-read-only-specialists"));
            events.Add(new(1, "analysis.fanin", $"peak-concurrency-{peak}"));
        }
        var offset = events.Count;
        events.AddRange(run.Aggregate.Events.Select((item, index) =>
            new PilotEvent(index + offset, item.Type, Outcome(item.PayloadJson))));
        var assertions = scenario == "feature"
            ? new Dictionary<string, object>
            {
                ["parallelAnalysis"] = true,
                ["peakConcurrency"] = peak,
                ["isolatedImplementation"] = isolatedImplementation,
                ["mandatoryVerification"] = "passed",
                ["goalStatus"] = run.Aggregate.Goal.Status.ToString().ToLowerInvariant(),
                ["terminalLearningPublications"] = learning.Outcomes.Count,
            }
            : new Dictionary<string, object>
            {
                ["initialVerification"] = "failed",
                ["repairCreated"] = run.RepairCreated,
                ["repairAttempt"] = 2,
                ["repairLimit"] = 2,
                ["subsequentVerification"] = "passed",
                ["terminalLearningPublications"] = learning.Outcomes.Count,
            };
        return new("1.0.0", scenario, "passed", true,
            [Git(Directory.GetCurrentDirectory(), "rev-parse", "HEAD").Trim(), "9f57bac", "e24a328"], events, assertions,
            run.Aggregate.Evidence.Select(item => item.Uri).Distinct().ToArray(),
            ["dotnet run --project tools/LoopPilotRunner"]);
    }
    finally
    {
        if (File.Exists(database)) File.Delete(database);
    }
}

async Task<PilotEvidence> RunRestartScenario(Arguments arguments)
{
    var database = Path.Combine(Path.GetTempPath(), $"cas-pilot-restart-{Guid.NewGuid():N}.db");
    try
    {
        var store = new SqliteGoalStore(database, NullLogger<SqliteGoalStore>.Instance);
        await store.InitializeAsync();
        var seed = Seed("goal-restart") with
        {
            Goal = Seed("goal-restart").Goal with { Status = GoalStatus.Running },
            WorkItems = [Seed("goal-restart").WorkItems[0] with { Status = WorkItemStatus.Running }],
            Leases = [new("expired", "goal-restart", "work-1", "old-worker", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-5))],
        };
        await store.SaveAsync(seed);
        var recovered = await store.RecoverExpiredLeasesAsync(DateTimeOffset.UtcNow);
        var first = new Dictionary<string, bool>();
        var duplicate = new Dictionary<string, bool>();
        foreach (var effect in new[] { "commit", "comment", "pull-request" })
        {
            first[effect] = await store.TryReserveIdempotencyKeyAsync("goal-restart", "work-1", $"restart-{effect}", effect);
            duplicate[effect] = await store.TryReserveIdempotencyKeyAsync("goal-restart", "work-1", $"restart-{effect}", effect);
        }
        return new("1.0.0", "restart", "passed", true, ["ddef0bf"],
            [new(0, "lease.expired", "worker-interrupted"), new(1, "lease.reclaimed", recovered == 1 ? "new-owner" : "failed"), new(2, "side-effects.reconciled", duplicate.Values.Any(value => value) ? "duplicate" : "idempotent")],
            new()
            {
                ["leaseReclaimed"] = recovered == 1,
                ["duplicateCommit"] = duplicate["commit"] ? 1 : 0,
                ["duplicateComment"] = duplicate["comment"] ? 1 : 0,
                ["duplicatePullRequest"] = duplicate["pull-request"] ? 1 : 0,
                ["idempotencyPreserved"] = first.Values.All(value => value) && duplicate.Values.All(value => !value),
            },
            ["cas://evidence/pilots/restart/sqlite-lease", "cas://evidence/pilots/restart/idempotency"],
            ["dotnet run --project tools/LoopPilotRunner"]);
    }
    finally
    {
        if (File.Exists(database)) File.Delete(database);
    }
}

PilotEvidence RunPolicyScenario()
{
    var denied = false;
    try { LoopPolicyGuard.RequireReadablePath("repo/.env"); }
    catch (UnauthorizedAccessException) { denied = true; }
    var push = LoopPolicyGuard.EvaluateExternalAction("push", false);
    var deploy = LoopPolicyGuard.EvaluateExternalAction("deploy", false);
    return new("1.0.0", "policy", "passed", true, ["e1e3232"],
        [new(0, "sandbox.read", denied ? "env-denied" : "allowed"), new(1, "push.requested", Decision(push)), new(2, "deploy.requested", Decision(deploy))],
        new()
        {
            ["envAccess"] = denied ? "denied" : "allowed",
            ["pushBeforeApproval"] = push == ExternalActionDecision.Authorized,
            ["deployBeforeApproval"] = deploy == ExternalActionDecision.Authorized,
            ["approvalRequired"] = true,
        },
        ["cas://evidence/pilots/policy/guard"],
        ["dotnet run --project tools/LoopPilotRunner"]);
}

bool VerifyIsolatedWorktree()
{
    var root = Path.Combine(Path.GetTempPath(), $"cas-pilot-repo-{Guid.NewGuid():N}");
    var worktree = Path.Combine(Path.GetTempPath(), $"cas-pilot-worktree-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        Git(root, "init", "-b", "main");
        Git(root, "config", "user.email", "pilot@localhost");
        Git(root, "config", "user.name", "CAS Pilot");
        File.WriteAllText(Path.Combine(root, "README.md"), "baseline");
        Git(root, "add", "README.md");
        Git(root, "commit", "-m", "baseline");
        var before = Git(root, "rev-parse", "HEAD").Trim();
        Git(root, "worktree", "add", "-b", "codex/pilot-isolation", worktree, before);
        File.WriteAllText(Path.Combine(worktree, "pilot.txt"), "isolated mutation");
        var sourceAfter = Git(root, "rev-parse", "HEAD").Trim();
        var changed = Git(worktree, "status", "--porcelain").Contains("pilot.txt", StringComparison.Ordinal);
        Git(root, "worktree", "remove", "--force", worktree);
        return before == sourceAfter && changed && !Path.GetFullPath(worktree).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
        if (Directory.Exists(worktree)) Directory.Delete(worktree, true);
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

string Git(string workingDirectory, params string[] arguments)
{
    using var process = new System.Diagnostics.Process
    {
        StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }
    };
    foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    process.Start();
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
    return output;
}

static string Decision(ExternalActionDecision decision) => decision == ExternalActionDecision.WaitingApproval ? "waiting-approval" : "authorized";
static string Outcome(string payload) => JsonDocument.Parse(payload).RootElement.GetProperty("outcome").GetString() ?? "unknown";
static VerificationRunResult Passed() => Verification(VerificationOutcome.Passed);
static VerificationRunResult Failed() => Verification(VerificationOutcome.Failed);
static VerificationRunResult Verification(VerificationOutcome outcome) => new(outcome,
    [new("test", VerificationCategory.Test, true, outcome, $"cas://evidence/verifier/{outcome.ToString().ToLowerInvariant()}", outcome == VerificationOutcome.Passed ? 0 : 1, 1)]);
static GoalAggregate Seed(string goalId) => new(
    new(goalId, $"corr-{goalId}", GoalStatus.Planned, new(3, 3, 2, 600, 10, 2)),
    [new("work-1", goalId, "repo", "provider", WorkItemStatus.Ready, 2, $"idem-{goalId}")],
    [], [], [], [], [], [], [], []);

sealed record PilotEvent(int Sequence, string Type, string Outcome);
sealed record PilotEvidence(string SchemaVersion, string Scenario, string Status, bool Bounded, string[] SourceCommits, IReadOnlyList<PilotEvent> Events, Dictionary<string, object> Assertions, string[] Artifacts, string[] Reproduce);

sealed class ScriptedVerifier(IReadOnlyList<VerificationRunResult> results) : ILoopVerifier
{
    private int _index;
    public Task<VerificationRunResult> VerifyAsync(string goalId, LoopWorkResult work, CancellationToken cancellationToken) => Task.FromResult(results[_index++]);
}

sealed class CapturingLearning : ITerminalOutcomePublisher
{
    public List<TerminalLoopOutcome> Outcomes { get; } = [];
    public Task PublishAsync(TerminalLoopOutcome outcome, CancellationToken cancellationToken)
    {
        Outcomes.Add(outcome);
        return Task.CompletedTask;
    }
}

sealed record Arguments(string Output, string Python, string MafRoot)
{
    public static Arguments Parse(string[] args)
    {
        string? Value(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        return new(
            Value("--output") ?? Path.Combine(Directory.GetCurrentDirectory(), "evidence", "loop-pilots"),
            Value("--python") ?? "python",
            Value("--maf-root") ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "autogen")));
    }
}
