# Pitfalls Research — GitHub Portfolio

**Project:** Enterprise GitHub Portfolio (Coding-Autopilot-System org)
**Researched:** 2026-05-21
**Confidence:** HIGH (stable, well-documented patterns; no external fetch required)

---

## Immediate Red Flags (hiring manager dismisses in <30 seconds)

### 1. Default README or No README
**What goes wrong:** Repo has only the auto-generated "# repo-name" stub, or no README at all.
**Signal sent:** Author doesn't care about communication; project is a throwaway.
**Prevention:** Every public repo must have a README with at minimum: what it does, why it exists, how to run it.

### 2. Zero Commit Activity for 12+ Months
**What goes wrong:** The commit graph is empty or shows a burst then silence.
**Signal sent:** Project is dead; author moved on or couldn't finish.
**Prevention:** Even documentation commits count. A v1.0.0 release with a recent tag date signals intentional shipping, not abandonment.

### 3. All Private Repos
**What goes wrong:** Profile shows "0 public repositories" or one repo with no activity.
**Signal sent:** Nothing to evaluate. Hiring manager moves to the next candidate.
**Prevention:** The three existing projects are already public — this is solved. The org profile must make them findable.

### 4. Broken CI Badge (red "failing" or missing)
**What goes wrong:** README shows a red badge or a badge that renders as a broken image.
**Signal sent:** Author set up CI but the build is broken and they don't maintain it — worse than no badge at all.
**Prevention:** Never add a badge until the build passes. Validate badge URL format before committing. For .NET: `dotnet build` must exit 0 before badge goes in.

### 5. No Topics / No Description
**What goes wrong:** Repo has no description field and no topic tags. GitHub search returns nothing.
**Signal sent:** Author doesn't understand discoverability or doesn't care whether the work is found.
**Prevention:** Every repo needs a one-sentence description and 5-8 relevant topics (e.g., `dotnet`, `csharp`, `ai-agents`, `mcp`, `autonomous-agents`, `llm`).

### 6. Inconsistent or Childish Username/Org Name
**What goes wrong:** Org is named something like "test-stuff-123" or personal account has a gaming handle.
**Signal sent:** Not a professional.
**Prevention:** Already addressed — Coding-Autopilot-System is professional. OgeonX-Ai is acceptable for senior AI roles.

### 7. Repository Name Mismatches the Content
**What goes wrong:** Repo named "my-project" or "test" with no description.
**Signal sent:** Disorganized, no care for audience.
**Prevention:** Names like `gsd-orchestrator`, `promptimprover`, `autogen` are already correct — descriptive and lowercase-kebab.

---

## README Pitfalls

### 1. Wall of Text, No Structure
**What goes wrong:** Single paragraph blob with no headings, no code blocks, no badges.
**Signal sent:** Author cannot structure information — a core senior engineering skill.
**Prevention:** Use H2 sections: Overview, Architecture, Getting Started, Configuration, How It Works. Use code blocks for all commands.

### 2. "TODO: Add description here" Left in Place
**What goes wrong:** Template placeholders not filled in.
**Signal sent:** Sloppy; didn't review before pushing.
**Prevention:** Search for "TODO", "FIXME", "placeholder", "coming soon" before finalizing any README.

### 3. Screenshot/GIF That Shows an Error
**What goes wrong:** Demo gif or screenshot was captured during a broken state.
**Signal sent:** Author doesn't validate their own demos.
**Prevention:** Re-capture all visuals in a clean working state. For this project: Mermaid diagrams render in GitHub — no screenshots needed.

### 4. Mermaid Syntax Errors
**What goes wrong:** Mermaid diagram block is malformed; GitHub renders it as a fenced code block with raw text.
**Signal sent:** Author added a "diagram" without verifying it renders.
**Prevention:** Validate every Mermaid block at https://mermaid.live before committing. Use only confirmed-supported diagram types (flowchart, sequenceDiagram, classDiagram work reliably on GitHub).

### 5. "This is a learning project" or "just a demo" Language
**What goes wrong:** Self-deprecating framing in the opening paragraph.
**Signal sent:** Author doesn't believe in their own work; confirms it's not production-grade.
**Prevention:** Write from the perspective of the system's capabilities, not its origin story. "gsd-orchestrator automates GitHub workflows using AI agents" not "I built this to learn agentic systems."

### 6. Missing Prerequisites / Broken "Getting Started"
**What goes wrong:** Setup steps assume too much context, skip environment variables, or reference paths that don't exist.
**Signal sent:** Author hasn't tried running their own instructions.
**Prevention:** Every README must list exact prerequisites (SDK version, required env vars, dependencies). For this project: .NET 10 SDK, Node.js version, any required API keys must be named (not valued).

### 7. Badges That Link to Nothing or Wrong Repo
**What goes wrong:** Badge URL has the wrong owner/repo path. Clicking it 404s or shows another repo's status.
**Signal sent:** Copy-paste without review.
**Prevention:** Construct badge URLs programmatically from `${{ github.repository }}` conventions. Test every badge link after adding it.

### 8. No License Specified
**What goes wrong:** No LICENSE file. GitHub shows "No license" warning.
**Signal sent:** Author doesn't understand open source basics; legal ambiguity deters employers from forking/referencing the work.
**Prevention:** MIT is the right default for a portfolio project. One LICENSE file per repo.

---

## CI/CD Pitfalls

### 1. Workflow That Never Runs
**What goes wrong:** Workflow triggers only on `workflow_dispatch` or on a branch that doesn't exist (`main` when default branch is `master`, or vice versa).
**Signal sent:** CI exists as theater — the badge is always grey ("no status").
**Prevention:** Always trigger on `push` to the default branch AND `pull_request`. Verify branch name matches `github.event.repository.default_branch` or explicitly match the correct name.

### 2. Pinning to `@latest` or Floating Major Versions
**What goes wrong:** `uses: actions/checkout@main` or `uses: actions/setup-dotnet@v3` without a patch pin.
**Signal sent:** Workflow will break silently when upstream changes; not how production CI is written.
**Prevention:** Pin to full SHA or at minimum a specific minor version. Prefer `actions/checkout@v4`, `actions/setup-dotnet@v4`. For this portfolio, major version pins are acceptable — SHA pins signal paranoia without benefit at this scale.

### 3. Installing Dependencies Every Run Without Caching
**What goes wrong:** `npm install` or `dotnet restore` runs from scratch on every push. Builds take 3-5 minutes for trivial changes.
**Signal sent:** Author doesn't know basic CI optimization.
**Prevention:**
- .NET: Use `actions/cache` keyed on `**/*.csproj` and `**/packages.lock.json`
- Node.js/TypeScript: Use `actions/setup-node` with `cache: 'npm'` built-in
- Python: Use `actions/setup-python` with `cache: 'pip'`

### 4. Hardcoded Tool Versions That Diverge from Repo
**What goes wrong:** Workflow installs .NET 6 but the `.csproj` targets `net10.0`.
**Signal sent:** CI was copied from a template and not adapted.
**Prevention:** Read the `<TargetFramework>` from the csproj and match the SDK version exactly. For this project: .NET 10 requires `dotnet-version: '10.x'`.

### 5. Build Succeeds But Artifacts Are Useless
**What goes wrong:** Workflow runs `dotnet build` but doesn't specify `--configuration Release`, uploads no artifacts, does nothing a developer would actually need.
**Signal sent:** Checkbox CI — looks good, provides no value.
**Prevention:** For a portfolio .NET project: `dotnet build --configuration Release --no-restore` after restore step. Upload artifacts only if there is a release workflow; otherwise just build+report.

### 6. Failing Build Left Unresolved
**What goes wrong:** Badge shows red for days or weeks.
**Signal sent:** Author either doesn't know it's failing or doesn't care.
**Prevention:** Fix build failures within one working day. For the three projects: ensure build passes locally before adding CI. Never add a CI workflow to a repo that doesn't currently build.

### 7. Workflow Runs on Every Branch Including Forks
**What goes wrong:** `on: push` without branch filter triggers on all forks' pushes, consuming quota and potentially exposing secrets.
**Signal sent:** Not an issue for a personal portfolio, but signals lack of workflow hygiene.
**Prevention:** Scope triggers: `on: push: branches: [main]` and `on: pull_request: branches: [main]`.

---

## Security Pitfalls in GitHub Actions

**Confidence: HIGH** — sourced from official GitHub security hardening documentation (stable since 2022, reinforced in 2024 advisory).

### 1. Secrets in Environment Variables at Job Level
**What goes wrong:** Developer writes `env: MY_SECRET: ${{ secrets.API_KEY }}` at the job or workflow level, making it available to all steps including third-party actions.
**Consequences:** Any compromised action in the workflow can read all job-level env vars.
**Prevention:** Declare secrets only in the specific `step` that needs them, not at job or workflow level.

### 2. Printing Secrets to Logs
**What goes wrong:** `echo "Token: ${{ secrets.TOKEN }}"` or `run: env` which dumps all env vars including secrets.
**Consequences:** Secret value appears in public workflow logs. GitHub masks known secret values but only if they match exactly — partial values or encoded variants are not masked.
**Prevention:** Never echo secrets. Never run `env` or `printenv` in a step that has secrets set. Use `${{ secrets.NAME }}` only in `with:` or `env:` fields of steps, never in `run:` string interpolation directly.

### 3. GITHUB_TOKEN Over-Permission
**What goes wrong:** Workflow uses default `GITHUB_TOKEN` permissions (which were historically read+write for all scopes) without restricting them.
**Consequences:** A compromised workflow step can write to any ref, create releases, modify issues — far beyond what the job requires.
**Prevention:** Set minimum permissions explicitly at workflow level:
```yaml
permissions:
  contents: read
```
Then override per-job only for jobs that need write access (e.g., `contents: write` for a release job). For a simple CI build that only validates, `contents: read` is sufficient.

### 4. Using `pull_request_target` Without Caution
**What goes wrong:** `on: pull_request_target` runs in the context of the base repo (with secrets) even for PRs from forks. If the workflow checks out the PR head ref and runs it, fork code executes with repo secrets.
**Consequences:** Arbitrary code execution with access to all secrets.
**Prevention:** For a personal portfolio where all PRs come from the owner, this is low risk. Still: do not use `pull_request_target` for workflows that run user-supplied code. Use `pull_request` (which runs without secrets for fork PRs) for build/test workflows.

### 5. Third-Party Actions Without Version Pinning
**What goes wrong:** `uses: some-org/some-action@main` — the action can change at any time, including maliciously if the upstream account is compromised.
**Consequences:** Supply chain attack; malicious code runs in the workflow with access to whatever the job can access.
**Prevention:** Pin to a commit SHA for third-party actions you don't control: `uses: some-org/some-action@abc123def456`. For GitHub's own first-party actions (`actions/*`), major version tags are acceptable.

### 6. Secrets in Commit Messages, Branch Names, or PR Titles
**What goes wrong:** Developer accidentally pastes an API key into a commit message or PR title when debugging.
**Consequences:** Secret is permanently in git history and GitHub's search index.
**Prevention:** Use `.env.example` files with placeholder values. Never commit `.env` files. Add `.env` and `*.env` to `.gitignore` before the first commit.

### 7. No `permissions:` Block = Implicit Broad Access
**What goes wrong:** Omitting the `permissions:` block means the workflow inherits the repository's default token permissions. GitHub changed the default to `read-all` in 2023 for new repos, but existing repos or org-level overrides may still default to `write-all`.
**Prevention:** Always include an explicit `permissions:` block. Signals to senior reviewers that the author understands least-privilege. For this portfolio's CI workflows:
```yaml
permissions:
  contents: read
```

---

## Wiki Pitfalls

### 1. Empty Wiki with Only the Default "Welcome" Page
**What goes wrong:** Wiki is enabled but contains only `# Welcome to the [repo] wiki`. GitHub shows this page to visitors.
**Signal sent:** Feature enabled as checkbox; no follow-through.
**Prevention:** Either populate the wiki before enabling it publicly, or don't enable it until content is ready. For this project: create minimum 4 substantive pages per repo before the wiki is linked from the README.

### 2. Wiki That Duplicates the README
**What goes wrong:** Wiki "Overview" page is a copy-paste of the README.
**Signal sent:** Author doesn't know what belongs where.
**Prevention:** README = entry point (what/why/quick start). Wiki = depth (architecture decisions, configuration reference, contributing guide, operational runbooks). No duplication.

### 3. Broken Internal Wiki Links
**What goes wrong:** Wiki page links to `[[Architecture]]` but the page is named `Architecture-Overview`. GitHub wiki links are case-sensitive and space-sensitive.
**Signal sent:** Author didn't click their own links.
**Prevention:** Use exact page names in links. Wiki page names use hyphens, not spaces, in the URL. Test every link after creating pages.

### 4. Out-of-Date Version Numbers or Screenshots
**What goes wrong:** Wiki says "requires .NET 8" after the project has been upgraded.
**Signal sent:** Docs are not maintained; can't be trusted.
**Prevention:** Version numbers in wikis should be kept minimal or stated as "see README for current requirements." Architecture concepts age better than specific version callouts.

### 5. Wiki Not Linked from README
**What goes wrong:** Wiki exists but the README has no mention of it. Visitors don't discover deeper documentation.
**Signal sent:** Either the wiki is an afterthought or the author doesn't think it's valuable.
**Prevention:** Add a "Documentation" section to every README with a direct link to the wiki's home page. Example: `[Full documentation →](https://github.com/Coding-Autopilot-System/gsd-orchestrator/wiki)`

### 6. Single Massive Wiki Page
**What goes wrong:** All documentation dumped into one page with no navigation.
**Signal sent:** Author understands content creation but not information architecture.
**Prevention:** Each wiki page covers one topic. Use the sidebar for navigation. Recommended structure for gsd-orchestrator: Home, Architecture, Configuration-Reference, State-Machine-Design, Extending-the-Orchestrator.

---

## Signals of Abandonment

### 1. Last Commit Date Is the Only Activity Signal
**What goes wrong:** The repo was created, populated with code, and never touched again. GitHub shows "committed 14 months ago."
**Signal sent:** Project is not maintained; code may be broken or stale.
**Prevention:** A v1.0.0 release reframes abandonment as "shipped." A GitHub Release with release notes signals intentional completion, not stagnation. The commit date matters less when there's a tagged release.

### 2. No Releases, No Tags
**What goes wrong:** All work lives in commits with no versioned checkpoints.
**Signal sent:** Author has no release discipline — can't tell what's "done."
**Prevention:** Create at least one release (`v1.0.0`) for each repo. Release notes don't need to be exhaustive — a three-sentence summary of what the system does is sufficient.

### 3. Open Issues That Are Obviously Bugs
**What goes wrong:** Issue tracker shows "TypeError: cannot read property of undefined" with zero response from the author.
**Signal sent:** Project is broken and unmaintained.
**Prevention:** Either close stale issues with a note, fix them, or use the label "known-limitation" to signal awareness. An empty issue tracker is better than one full of unresponded-to bugs.

### 4. Dependencies That Are Years Out of Date
**What goes wrong:** `package.json` shows `"typescript": "^4.0.0"` when TypeScript 5.x has been current for years. `*.csproj` references packages from 2022.
**Signal sent:** Author doesn't maintain their own dependencies.
**Prevention:** Before portfolio launch: run `dotnet outdated` (or `npm outdated`) and update at least the major dependencies. For portfolio purposes, note in the README: "Dependencies current as of [date]."

### 5. Default Branch Named `master` with No Work Done After Rename Convention Settled
**What goes wrong:** Main branch is `master` on a project created recently. GitHub has defaulted to `main` since 2020.
**Signal sent:** Either auto-created years ago and untouched, or author is unaware of current conventions.
**Prevention:** Rename to `main` if it's currently `master`. This also makes CI workflows cleaner.

### 6. Zero Stars / Watchers on the Org
**Not a pitfall to fix** — stars are a vanity metric and hiring managers who understand GitHub know that personal/professional portfolio orgs rarely have stars. Don't pursue stars artificially.

### 7. Forked Repos Dominating the Profile
**What goes wrong:** Profile shows 12 repos, 9 of which are forks of popular projects with zero modifications.
**Signal sent:** Author hasn't built anything; just collecting.
**Prevention:** Pin only original work to the profile. Unpin all forks. For this project: the org profile should pin the three original repos, nothing else.

---

## Phase Mapping

| Pitfall | Applies To Phase | Mitigation |
|---------|-----------------|------------|
| Broken CI badge | Phase: CI setup (gsd-orchestrator, Promptimprover, autogen) | Build must pass locally before badge is added |
| Secrets in workflows | Phase: CI setup — all repos | Explicit `permissions: contents: read` block in every workflow |
| GITHUB_TOKEN over-permission | Phase: CI setup | Add permissions block; only release workflow gets `contents: write` |
| Mermaid syntax error | Phase: Architecture diagram (gsd-orchestrator) | Validate at mermaid.live before commit |
| Empty wiki | Phase: Wiki creation (all repos) | Create all pages before linking from README |
| Broken wiki links | Phase: Wiki creation | Click every link after page creation |
| README "learning project" language | Phase: README update (all repos) | Review opening paragraph for self-deprecating framing |
| Missing topics/description | Phase: Org cleanup (early) | Set description + 5-8 topics per repo before other work |
| No releases/tags | Phase: Release (gsd-orchestrator v1.0.0) | Create release after CI passes; write substantive release notes |
| Stale dependencies | Phase: README update | Note dependency currency date; run `dotnet outdated` audit |
| Wiki duplicates README | Phase: Wiki creation | README = entry point; Wiki = depth; no copy-paste |
| Workflow triggers wrong branch | Phase: CI setup | Match trigger branch to actual default branch name |
| No LICENSE file | Phase: Repo cleanup (early) | Add MIT LICENSE to all three repos |
| Dependency caching missing | Phase: CI setup | Use built-in cache options in setup-dotnet/setup-node/setup-python |
| Over-engineering signals | All phases | No Kubernetes configs, no Terraform, no multi-cloud for a portfolio that doesn't deploy |

---

## Over-Engineering the Portfolio

A distinct failure mode for senior engineers: the portfolio tries to demonstrate every skill at once and ends up signaling insecurity rather than competence.

### What it looks like
- Dockerfile present but the service never needs to be containerized for its stated purpose
- Kubernetes manifests in a repo for a CLI tool
- A `CONTRIBUTING.md` with 400 lines of contributor covenant for a solo project
- Terraform modules for infrastructure that doesn't exist
- A monorepo with packages/apps/libs directories for what is a single-purpose tool
- GitHub Projects board with 60 tickets and all of them "In Progress"
- Architecture diagrams with 15+ components where 3 would be accurate

### Why it backfires
Senior hiring managers pattern-match on this immediately: it's compensation for insecurity, not demonstration of capability. The goal is "this person could build this at my company" — not "this person owns every possible tool."

### Prevention for this project
- gsd-orchestrator's architecture diagram should show the actual components: state machine, MCP client, GitHub API, Claude. Not AWS, not Kubernetes, not a service mesh.
- Wiki pages should document what the system actually does, not what it could theoretically scale to.
- CI workflows should be the minimum that validates the code: restore, build, (test if tests exist). No SonarQube, no DAST, no container scanning for a portfolio project.

---

## Sources

- GitHub Actions security hardening documentation (official) — HIGH confidence
- GitHub Docs: Using secrets in GitHub Actions — HIGH confidence
- GitHub Docs: Automatic token authentication — HIGH confidence
- GitHub Docs: Workflow syntax for GitHub Actions (permissions key) — HIGH confidence
- GitHub community discussions on portfolio best practices — MEDIUM confidence (training data, well-corroborated patterns)
- Industry hiring manager feedback patterns — MEDIUM confidence (training data synthesis)

**Note:** External fetching was unavailable in this research session (WebSearch and WebFetch permissions denied; ctx7 CLI path resolution broken on Windows). All findings are based on training data current to August 2025, covering well-established, stable patterns. GitHub Actions security guidance has been stable since 2022; pitfalls listed are confirmed in official documentation from that period. Confidence is HIGH for security/CI patterns, MEDIUM for hiring manager behavior patterns.
