# GitHub MCP Server — local SSE startup script
# Runs at Windows logon via Task Scheduler (registered by install-autostart.ps1)
# Serves all AI CLI tools (Claude Code, Gemini CLI, Codex CLI) at http://localhost:8765/sse

$ErrorActionPreference = "Stop"

# ── Read GITHUB_TOKEN from .env ───────────────────────────────────────────────
$envFile = "C:\GithubMCP\.env"
if (-not (Test-Path $envFile)) {
    Write-Error "Missing .env file at $envFile. Copy .env.example and fill in GITHUB_TOKEN."
    exit 1
}

$token = Get-Content $envFile |
    Where-Object { $_ -match "^GITHUB_TOKEN=(.+)" } |
    ForEach-Object { $Matches[1].Trim() } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($token) -or $token -eq "ghp_your_token_here") {
    Write-Error "GITHUB_TOKEN not set in $envFile"
    exit 1
}

# ── Ensure Node.js is in PATH ─────────────────────────────────────────────────
$nodePath = "C:\Program Files\nodejs"
if (-not ($env:PATH -like "*$nodePath*")) {
    $env:PATH = "$nodePath;$env:PATH"
}

# ── Start the MCP server ──────────────────────────────────────────────────────
$env:GITHUB_TOKEN = $token
Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Starting GitHub MCP Server on http://localhost:8765/sse"

# github-mcp-server is expected to be globally installed (npm install -g @github/github-mcp-server)
# Falls back to npx if not found
$binary = Get-Command "github-mcp-server" -ErrorAction SilentlyContinue
if ($binary) {
    & github-mcp-server sse --port 8765
} else {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] global install not found, using npx (slower first start)"
    & npx -y "@github/github-mcp-server" sse --port 8765
}
