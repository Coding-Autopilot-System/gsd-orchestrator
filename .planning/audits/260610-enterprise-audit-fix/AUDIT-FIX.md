# Enterprise Audit-Fix Report

**Date:** 2026-06-10
**Source:** `gsd-audit-fix --severity all --max 8`
**Scope:** .NET correctness, workflow durability, security, GitHub integration, observability, tests, CI, and documentation.

## Classification

| ID | Finding | Severity | Classification | Status |
|---|---|---|---|---|
| F-01 | CI builds only the production project and never runs tests | high | auto-fixable | blocked: GitHub token lacks workflow scope |
| F-02 | Test generation accepts blank tool content and crashes on empty LLM messages | high | auto-fixable | fixed after validation repair |
| F-03 | Editing state accepts blank tool content and crashes on empty LLM messages | high | auto-fixable | not attempted after pipeline stop |
| F-04 | Workflow states use null-forgiving context dereferences instead of diagnostic guards | medium | auto-fixable | not attempted after pipeline stop |
| F-05 | Checkpoint writes reuse a predictable temporary filename and can collide | medium | auto-fixable | not attempted after pipeline stop |
| F-06 | Watch mode examines only 20 issues and evicts processed issues nondeterministically | medium | auto-fixable | not attempted after pipeline stop |
| F-07 | MCP pending requests may hang when the child process exits cleanly | medium | auto-fixable | not attempted after pipeline stop |
| F-08 | Repo configuration accepts empty owner/repo values and invalid delays | low | auto-fixable | not attempted after pipeline stop |

## Manual-Only Findings

- Replace the committed `github-mcp-server.exe` with a reproducible, checksum-verified acquisition or release packaging strategy.
- Decide the production authentication model. The current local PAT model conflicts with the target enterprise managed-identity/OAuth posture.
- Run live end-to-end GitHub and LLM workflow UAT; mocked tests cannot prove external API behavior, prompt robustness, or reviewer permissions.

## Fix Evidence

- F-01: The fix was validated locally, then reverted because GitHub rejected workflow updates from the active OAuth token without `workflow` scope.
- F-02: Blank generated test content is skipped and empty LLM response collections are handled safely; regression coverage added.
- Final local validation: `dotnet build GithubMCP.slnx --configuration Release --no-restore`, `dotnet test GithubMCP.slnx --configuration Release --no-build`, and `git diff --check`.

## Pipeline Stop

The first F-02 edit failed compilation because a patch transport inserted literal newline escapes. The finding was repaired immediately and the full suite returned green. In accordance with `gsd-audit-fix`, remaining findings were marked not-attempted rather than continuing after a failed validation.