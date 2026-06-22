using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Mcp;

/// <summary>
/// Communicates with the GitHub MCP Server over JSON-RPC 2.0 via stdin/stdout.
/// One JSON object per line. Responses are correlated to requests by the "id" field.
/// The server process is spawned on InitializeAsync and disposed with this client.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Subprocess stdio wrapper � requires live MCP binary; covered by integration tests")]
public sealed class McpStdioClient : IMcpClient
{
    private readonly string _githubToken;
    private readonly string _binaryPath;
    private readonly ILogger<McpStdioClient> _logger;

    private Process? _process;
    private StreamWriter? _stdin;
    private readonly Channel<JsonObject> _responseChannel =
        Channel.CreateUnbounded<JsonObject>(new UnboundedChannelOptions { SingleReader = false });

    private readonly Dictionary<string, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private long _nextId;
    private Task? _readerTask;
    private CancellationTokenSource? _readerCts;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public McpStdioClient(string githubToken, string binaryPath, ILogger<McpStdioClient> logger)
    {
        _githubToken = githubToken;
        _binaryPath = binaryPath;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _binaryPath,
                Arguments = "stdio",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["GITHUB_PERSONAL_ACCESS_TOKEN"] = _githubToken }
            },
            EnableRaisingEvents = true
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _logger.LogDebug("[mcp-stderr] {Line}", e.Data);
        };

        _process.Start();
        _process.BeginErrorReadLine();
        _stdin = _process.StandardInput;

        _readerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readerTask = ReadLoopAsync(_readerCts.Token);

        // MCP initialize handshake
        var initResult = await SendRequestAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject
            {
                ["name"] = "gsd-orchestrator",
                ["version"] = "1.0.0"
            }
        }, ct);

        var serverName = initResult?["result"]?["serverInfo"]?["name"]?.GetValue<string>() ?? "unknown";
        _logger.LogInformation("MCP server connected: {ServerName}", serverName);

        // Send required initialized notification (no response expected)
        await SendNotificationAsync("notifications/initialized", new JsonObject(), ct);
    }

    public async Task<IReadOnlyList<McpTool>> ListToolsAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync("tools/list", new JsonObject(), ct);
        var toolsArray = result?["result"]?["tools"]?.AsArray()
            ?? throw new McpException("tools/list returned no tools array");

        return toolsArray
            .Select(t => new McpTool(
                Name: t!["name"]!.GetValue<string>(),
                Description: t["description"]?.GetValue<string>() ?? "",
                InputSchema: t["inputSchema"]?.AsObject() ?? new JsonObject()))
            .ToList();
    }

    public async Task<McpToolResult> CallToolAsync(string name, JsonObject arguments, CancellationToken ct = default)
    {
        _logger.LogDebug("MCP call: {Tool}", name);

        var response = await SendRequestAsync("tools/call", new JsonObject
        {
            ["name"] = name,
            ["arguments"] = arguments
        }, ct);

        var resultNode = response?["result"]
            ?? throw new McpException($"MCP returned no result for tool '{name}'");

        bool isError = resultNode["isError"]?.GetValue<bool>() ?? false;
        var contentArray = resultNode["content"]?.AsArray();
        var text = contentArray?.FirstOrDefault()?["text"]?.GetValue<string>() ?? "";

        if (isError)
        {
            _logger.LogWarning("MCP tool '{Tool}' returned error: {Error}", name, text);
            throw McpException.FromToolError(text);
        }

        return new McpToolResult(text, isError);
    }

    // ── Internal plumbing ────────────────────────────────────────────────────

    private async Task<JsonObject?> SendRequestAsync(string method, JsonObject @params, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextId).ToString();
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = @params
        };

        var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending) _pending[id] = tcs;

        try
        {
            await WriteLineAsync(request.ToJsonString(JsonOpts), ct);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new McpException($"MCP request '{method}' timed out after 30s", isTransient: true);
        }
        finally
        {
            lock (_pending) _pending.Remove(id);
        }
    }

    private async Task SendNotificationAsync(string method, JsonObject @params, CancellationToken ct)
    {
        var notification = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = @params
        };
        await WriteLineAsync(notification.ToJsonString(JsonOpts), ct);
    }

    private async Task WriteLineAsync(string line, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            await _stdin!.WriteLineAsync(line.AsMemory(), ct);
            await _stdin.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var stdout = _process!.StandardOutput;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await stdout.ReadLineAsync(ct);
                if (line is null) break; // process exited

                if (string.IsNullOrWhiteSpace(line)) continue;

                JsonObject? msg;
                try { msg = JsonNode.Parse(line)?.AsObject(); }
                catch
                {
                    _logger.LogWarning("MCP: failed to parse line: {Line}", line);
                    continue;
                }

                if (msg is null) continue;

                var id = msg["id"]?.GetValue<string>();
                if (id is null) continue; // notification, ignore

                TaskCompletionSource<JsonObject>? tcs;
                lock (_pending) _pending.TryGetValue(id, out tcs);
                tcs?.TrySetResult(msg);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP reader loop failed");
            // Fail all pending requests
            lock (_pending)
            {
                foreach (var tcs in _pending.Values)
                    tcs.TrySetException(new McpException("MCP process died unexpectedly", isTransient: true));
                _pending.Clear();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _readerCts?.Cancel();
        if (_readerTask is not null)
        {
            try { await _readerTask; } catch { /* ignore */ }
        }

        try { _stdin?.Close(); } catch { /* ignore */ }

        if (_process is not null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
                await _process.WaitForExitAsync();
            }
            catch { /* ignore */ }
        }

        _process?.Dispose();
        _readerCts?.Dispose();
        _writeLock.Dispose();
    }
}
