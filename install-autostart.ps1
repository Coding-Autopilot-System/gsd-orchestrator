# Run this ONCE to set up the GitHub MCP Server auto-start.
# Does NOT require admin rights — uses per-user Task Scheduler.
#
# Usage: powershell -ExecutionPolicy Bypass -File C:\GithubMCP\install-autostart.ps1

$ErrorActionPreference = "Stop"
$taskName  = "GitHub MCP Server (port 8765)"
$scriptPath = "C:\GithubMCP\start-mcp-server.ps1"

# ── 1. Install github-mcp-server globally (faster cold start than npx) ────────
Write-Host "Installing @github/github-mcp-server globally..."
npm install -g "@github/github-mcp-server"
Write-Host "Done."

# ── 2. Remove existing task if present ────────────────────────────────────────
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "Removed existing task."
}

# ── 3. Register Task Scheduler task ───────────────────────────────────────────
$action = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$scriptPath`""

# At logon of the current user — no admin / no credentials stored
$trigger = New-ScheduledTaskTrigger -AtLogon -User $env:USERNAME

$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `  # No time limit (runs until killed)
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew

Register-ScheduledTask `
    -TaskName  $taskName `
    -Action    $action `
    -Trigger   $trigger `
    -Settings  $settings `
    -RunLevel  Limited `
    -Force | Out-Null

Write-Host ""
Write-Host "✓ Task '$taskName' registered."
Write-Host "  Starts automatically at next Windows logon."
Write-Host ""
Write-Host "To start it NOW without rebooting:"
Write-Host "  Start-ScheduledTask -TaskName '$taskName'"
Write-Host ""
Write-Host "To remove auto-start:"
Write-Host "  Unregister-ScheduledTask -TaskName '$taskName' -Confirm:`$false"
