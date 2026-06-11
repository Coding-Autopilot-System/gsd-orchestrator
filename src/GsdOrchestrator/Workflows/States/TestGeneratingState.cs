using System.Text;
using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class TestGeneratingState : IWorkflowState
{
    private const int MaxTurnsPerFile = 20;

    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<TestGeneratingState> _logger;

    public WorkflowState State => WorkflowState.TestGenerating;

    public TestGeneratingState(McpToolDispatcher mcp, IChatClient llm, ILogger<TestGeneratingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var edits = ctx.Edits!;
        var issue = ctx.Issue!;
        var branch = ctx.Branch!;
        var generatedTests = new List<GeneratedTest>();

        var testablePaths = edits.Edits
            .Select(e => e.Path)
            .Where(IsTestableSourceFile)
            .ToList();

        if (testablePaths.Count == 0)
        {
            _logger.LogInformation("No testable source files in edits — skipping test generation");
            var empty = new TestGenerationContext([]);
            return (ctx with { TestGeneration = empty }).Transition(WorkflowState.Validating);
        }

        foreach (var sourcePath in testablePaths)
        {
            var testPath = DeriveTestPath(sourcePath);
            if (!sourcePath.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Non-standard source path {Path} — placing test in GsdOrchestrator.Tests root", sourcePath);
            }
            _logger.LogInformation("Generating tests: {SourcePath} → {TestPath}", sourcePath, testPath);
            var result = await GenerateTestFileAsync(issue, branch, sourcePath, testPath, ct);
            generatedTests.Add(result);
        }

        var testGenCtx = new TestGenerationContext(generatedTests);
        return (ctx with { TestGeneration = testGenCtx }).Transition(WorkflowState.Validating);
    }

    private static bool IsTestableSourceFile(string path)
    {
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".Tests\\", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("Spec.cs", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static string DeriveTestPath(string sourcePath)
    {
        sourcePath = sourcePath.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var testFileName = $"{fileName}Tests.cs";
        var parts = sourcePath.Split('/');
        if (parts.Length >= 2 && parts[0] == "src")
            return $"src/{parts[1]}.Tests/{testFileName}";
        return $"src/GsdOrchestrator.Tests/{testFileName}";
    }

    private async Task<GeneratedTest> GenerateTestFileAsync(
        IssueContext issue,
        BranchContext branch,
        string sourcePath,
        string testPath,
        CancellationToken ct)
    {
        var sourceContent = await ReadFileAsync(issue, branch, sourcePath, ct);
        var (existingTestContent, existingSha) = await TryReadFileWithShaAsync(issue, branch, testPath, ct);

        var writeFileTool = AIFunctionFactory.Create(
            (string content, string commitMessage) => Task.FromResult($"staged:{content.Length}"),
            "write_file",
            "Write the complete xUnit test file content. Call this when done generating tests.");

        var options = new ChatOptions
        {
            Tools = [writeFileTool],
            ToolMode = ChatToolMode.Auto,
            Temperature = 0.1f
        };

        var testClassName = Path.GetFileNameWithoutExtension(testPath);
        var systemPrompt = $$"""
            You are a C# test engineer. Generate xUnit 2.x tests for the provided source file.

            Rules:
            - Use xUnit [Fact] for single-scenario tests, [Theory] + [InlineData] for parameterized tests
            - Use NSubstitute (Substitute.For<T>()) for interface dependencies
            - Constructor-inject dependencies using the same pattern as the source class
            - Namespace: GsdOrchestrator.Tests
            - Class name: {{testClassName}}
            - One test class per source file
            - Tests must compile — use only types present in the source file and standard xUnit/NSubstitute APIs
            - If the source file has no testable public methods, call write_file with a single [Fact] placeholder test that asserts true
            - Do NOT add using directives for namespaces not referenced in the source

            Issue context (for understanding intent):
            Issue #{{issue.Number}}: {{issue.Title}}
            """;

        var existingSection = existingTestContent.Length > 0
            ? $$"""
                Existing tests (extend, do not duplicate):
                ```csharp
                {{existingTestContent}}
                ```
                """
            : "No existing test file — generate from scratch.";

        var userPrompt = $$"""
            Source file: {{sourcePath}}
            ```csharp
            {{sourceContent}}
            ```

            {{existingSection}}

            Generate comprehensive xUnit tests and call write_file with the complete test file content.
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        string? finalContent = null;
        int turns = 0;

        while (finalContent is null && turns < MaxTurnsPerFile)
        {
            turns++;
            var response = await _llm.GetResponseAsync(messages, options, ct);
            var lastMessage = response.Messages.LastOrDefault();
            if (lastMessage is null)
            {
                _logger.LogWarning("LLM returned no messages for {TestPath}, turn {Turn}", testPath, turns);
                break;
            }

            messages.Add(lastMessage);

            if (response.FinishReason == ChatFinishReason.ToolCalls)
            {
                foreach (var call in lastMessage.Contents.OfType<FunctionCallContent>())
                {
                    if (call.Name == "write_file")
                    {
                        var rawContent = call.Arguments?["content"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(rawContent))
                            finalContent = rawContent;
                        messages.Add(new ChatMessage(ChatRole.Tool,
                            [new FunctionResultContent(call.CallId, "File staged for commit.")]));
                    }
                }
            }
            else
            {
                _logger.LogWarning("LLM finished without calling write_file for {TestPath}, turn {Turn}", testPath, turns);
                break;
            }
        }

        if (finalContent is null)
        {
            _logger.LogWarning("Skipping {TestPath} — no content produced within {Max} turns", testPath, MaxTurnsPerFile);
            return new GeneratedTest(sourcePath, testPath, "", WasSkipped: true, "LLM produced no test content");
        }

        var commitArgs = new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["path"] = testPath,
            ["message"] = $"test(#{issue.Number}): generate xUnit tests for {Path.GetFileName(sourcePath)}",
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(finalContent)),
            ["branch"] = branch.BranchName
        };
        if (!string.IsNullOrEmpty(existingSha))
            commitArgs["sha"] = existingSha;

        var commitResult = await _mcp.CallAsync("create_or_update_file", commitArgs, ct);
        var newSha = commitResult.ParseInnerJson()?["content"]?["sha"]?.GetValue<string>() ?? "";

        _logger.LogInformation("Committed test file {TestPath} → {Sha}", testPath, newSha[..Math.Min(8, newSha.Length)]);
        return new GeneratedTest(sourcePath, testPath, newSha, WasSkipped: false, null);
    }

    private async Task<string> ReadFileAsync(IssueContext issue, BranchContext branch, string path, CancellationToken ct)
    {
        try
        {
            var result = await _mcp.CallAsync("get_file_contents", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["path"] = path,
                ["ref"] = branch.BranchName
            }, ct);
            var json = result.ParseInnerJson();
            var b64 = json?["content"]?.GetValue<string>()?.Replace("\n", "") ?? "";
            return b64.Length > 0 ? Encoding.UTF8.GetString(Convert.FromBase64String(b64)) : "";
        }
        catch (McpException)
        {
            _logger.LogInformation("File {Path} does not exist on branch — treating as empty", path);
            return "";
        }
    }

    private async Task<(string content, string sha)> TryReadFileWithShaAsync(
        IssueContext issue, BranchContext branch, string path, CancellationToken ct)
    {
        try
        {
            var result = await _mcp.CallAsync("get_file_contents", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["path"] = path,
                ["ref"] = branch.BranchName
            }, ct);
            var json = result.ParseInnerJson();
            var sha = json?["sha"]?.GetValue<string>() ?? "";
            var b64 = json?["content"]?.GetValue<string>()?.Replace("\n", "") ?? "";
            var content = b64.Length > 0 ? Encoding.UTF8.GetString(Convert.FromBase64String(b64)) : "";
            return (content, sha);
        }
        catch (McpException)
        {
            return ("", "");
        }
    }
}
