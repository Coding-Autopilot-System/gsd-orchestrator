using System.Text;
using System.Text.Json.Nodes;
using GsdOrchestrator.Mcp;
using GsdOrchestrator.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Workflows.States;

public sealed class EditingState : IWorkflowState
{
    private const int MaxTurnsPerFile = 20;

    private readonly McpToolDispatcher _mcp;
    private readonly IChatClient _llm;
    private readonly ILogger<EditingState> _logger;

    public WorkflowState State => WorkflowState.Editing;

    public EditingState(McpToolDispatcher mcp, IChatClient llm, ILogger<EditingState> logger)
    {
        _mcp = mcp;
        _llm = llm;
        _logger = logger;
    }

    public async Task<GsdWorkflowContext> ExecuteAsync(GsdWorkflowContext ctx, CancellationToken ct)
    {
        var issue = ctx.Issue!;
        var plan = ctx.Plan!;
        var branch = ctx.Branch!;
        var edits = new List<FileEdit>();

        foreach (var plannedFile in plan.FilesToModify)
        {
            _logger.LogInformation("Editing file: {Path}", plannedFile.Path);
            var edit = await EditFileAsync(issue, plan, branch, plannedFile, ct);
            if (edit is not null)
                edits.Add(edit);
        }

        return (ctx with { Edits = new EditContext(edits) }).Transition(WorkflowState.TestGenerating);
    }

    private async Task<FileEdit?> EditFileAsync(
        IssueContext issue,
        AnalysisPlan plan,
        BranchContext branch,
        PlannedFile plannedFile,
        CancellationToken ct)
    {
        // Read current file content
        string currentContent;
        string currentSha;
        try
        {
            var fileResult = await _mcp.CallAsync("get_file_contents", new JsonObject
            {
                ["owner"] = issue.RepoOwner,
                ["repo"] = issue.RepoName,
                ["path"] = plannedFile.Path,
                ["ref"] = branch.BranchName
            }, ct);

            var fileJson = fileResult.ParseInnerJson();
            currentSha = fileJson?["sha"]?.GetValue<string>() ?? "";

            // GitHub API returns content as base64
            var b64 = fileJson?["content"]?.GetValue<string>()?.Replace("\n", "") ?? "";
            currentContent = b64.Length > 0
                ? Encoding.UTF8.GetString(Convert.FromBase64String(b64))
                : "";
        }
        catch (McpException)
        {
            // File might not exist yet — will be created
            currentContent = "";
            currentSha = "";
            _logger.LogInformation("File {Path} does not exist — will create", plannedFile.Path);
        }

        // Build initial messages for ReAct loop
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, $"""
                You are a precise code editor. Your task:
                1. Read the issue and plan
                2. Modify the file content to resolve the issue
                3. When done, call the write_file tool with the complete new file content

                Issue #{issue.Number}: {issue.Title}
                {issue.Body}

                Plan: {plan.Summary}
                File to modify: {plannedFile.Path}
                Rationale: {plannedFile.Rationale}
                """),
            new(ChatRole.User, $"""
                Current content of {plannedFile.Path}:
                ```
                {currentContent}
                ```

                Analyze the issue, make the necessary changes, and call write_file with the complete updated content.
                """)
        };

        // Define the write_file synthetic tool so the LLM can signal completion
        var writeFileTool = AIFunctionFactory.Create(
            (string content, string commitMessage) => Task.FromResult($"staged:{content.Length}"),
            "write_file",
            "Write the complete updated file content to the branch. Call this when done editing.");

        var tools = new List<AITool> { writeFileTool };
        var options = new ChatOptions { Tools = tools, ToolMode = ChatToolMode.Auto, Temperature = 0.1f };

        string? finalContent = null;
        string finalCommitMessage = $"fix(#{issue.Number}): {plan.Summary}";
        int turns = 0;

        while (finalContent is null && turns < MaxTurnsPerFile)
        {
            turns++;
            var response = await _llm.GetResponseAsync(messages, options, ct);
            var lastMessage = response.Messages.Last();
            messages.Add(lastMessage);

            if (response.FinishReason == ChatFinishReason.ToolCalls)
            {
                foreach (var call in lastMessage.Contents.OfType<FunctionCallContent>())
                {
                    if (call.Name == "write_file")
                    {
                        finalContent = call.Arguments?["content"]?.ToString();
                        if (call.Arguments?.TryGetValue("commitMessage", out var cm) == true)
                            finalCommitMessage = cm?.ToString() ?? finalCommitMessage;

                        // Add tool result to message history
                        messages.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, "File staged for commit.")]));
                    }
                }
            }
            else
            {
                // LLM finished without calling write_file — extract content from response if possible
                _logger.LogWarning("LLM finished without calling write_file on {Path}, turn {Turn}", plannedFile.Path, turns);
                break;
            }
        }

        if (finalContent is null)
        {
            _logger.LogWarning("Skipping {Path} — no content produced within {Max} turns", plannedFile.Path, MaxTurnsPerFile);
            return null;
        }

        // Idempotency: check if content is already identical
        if (finalContent == currentContent && !string.IsNullOrEmpty(currentSha))
        {
            _logger.LogInformation("File {Path} unchanged — skipping commit", plannedFile.Path);
            return new FileEdit(plannedFile.Path, currentSha, currentSha, finalCommitMessage);
        }

        // Commit the file to the branch
        var commitArgs = new JsonObject
        {
            ["owner"] = issue.RepoOwner,
            ["repo"] = issue.RepoName,
            ["path"] = plannedFile.Path,
            ["message"] = finalCommitMessage,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(finalContent)),
            ["branch"] = branch.BranchName
        };
        if (!string.IsNullOrEmpty(currentSha))
            commitArgs["sha"] = currentSha;

        var commitResult = await _mcp.CallAsync("create_or_update_file", commitArgs, ct);
        var newSha = commitResult.ParseInnerJson()?["content"]?["sha"]?.GetValue<string>() ?? "";

        _logger.LogInformation("Committed {Path} → {Sha}", plannedFile.Path, newSha[..Math.Min(8, newSha.Length)]);
        return new FileEdit(plannedFile.Path, currentSha, newSha, finalCommitMessage);
    }
}
