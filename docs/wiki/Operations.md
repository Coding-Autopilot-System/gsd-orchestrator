# Operations

## Build

```bash
dotnet restore src/GsdOrchestrator/GsdOrchestrator.csproj
dotnet build src/GsdOrchestrator/GsdOrchestrator.csproj --no-restore --configuration Release
```

## Test

```bash
dotnet restore src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj
dotnet build src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj --no-restore --configuration Release
dotnet test src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj --configuration Release --no-build
```

Contract-compatibility gate only (consumer-side check against the pinned `cas-contracts` v1.1
release, fails red on drift):

```bash
dotnet test src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ContractCompatibilityTests"
```

## CI (`.github/workflows/ci.yml`, `windows-latest`, 30-minute timeout)

1. `actions/checkout@v7`
2. `actions/setup-dotnet@v5` (.NET `10.0.1xx`)
3. Restore + build `GsdOrchestrator.csproj`
4. Restore + build `GsdOrchestrator.Tests.csproj`
5. **Contract compatibility** — `ContractCompatibilityTests` filter, fails red on pinned-contract drift
6. **Test** — full suite with `--collect:"XPlat Code Coverage"`, results to `./TestResults`
7. **Upload coverage** — `actions/upload-artifact@v7`, `coverage-results` artifact (always runs)

There is currently **no coverage-threshold enforcement step** on `main` — coverage is
collected and published as an artifact only. A ratcheted `branch-rate` gate is in progress;
see [Architecture — Test Coverage](../../README.md#test-coverage) and PR #16.

A separate `.github/workflows/codeql.yml` runs CodeQL analysis (badge in the root `README.md`).

## Run the orchestrator locally

```bash
cd src/GsdOrchestrator
dotnet run -- --issue 42
dotnet run -- --resume <workflow-id>
```

Requires `.env` with `GITHUB_PERSONAL_ACCESS_TOKEN`, `ANTHROPIC_API_KEY`,
`GSD_GITHUB_OWNER`, `GSD_GITHUB_REPO` set (copy from `.env.example`).

<!-- docs-verified: a01b130c98cb7833d45cc7406f6002009f33557a 2026-07-08 -->
