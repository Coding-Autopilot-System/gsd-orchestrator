# GitHub MCP Server — local HTTP startup script
# Runs at Windows logon via Task Scheduler (registered by install-autostart.ps1)
# Also usable standalone: powershell -ExecutionPolicy Bypass -File C:\GithubMCP\start-mcp-server.ps1

$ErrorActionPreference = "Stop"

# ── Read token from .env ──────────────────────────────────────────────────────
$envFile = "C:\GithubMCP\.env"
if (-not (Test-Path $envFile)) {
    Write-Error "Missing .env file at $envFile. Copy .env.example and fill in GITHUB_PERSONAL_ACCESS_TOKEN."
    exit 1
}

$token = Get-Content $envFile |
    Where-Object { $_ -match "^GITHUB_PERSONAL_ACCESS_TOKEN=(.+)" } |
    ForEach-Object { $Matches[1].Trim() } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($token) -or $token -like "*your_token*") {
    Write-Error "GITHUB_PERSONAL_ACCESS_TOKEN not set in $envFile"
    exit 1
}

# ── Start the MCP server ──────────────────────────────────────────────────────
$env:GITHUB_PERSONAL_ACCESS_TOKEN = $token
$binary = "C:\GithubMCP\github-mcp-server.exe"

Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Starting GitHub MCP Server on http://localhost:8765"
& $binary http --port 8765 --toolsets all
