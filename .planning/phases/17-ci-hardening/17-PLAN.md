# Phase 17 — CI Hardening

## Goal

Add `dotnet test` with Coverlet coverage collection to the GitHub Actions CI workflow and add a tests badge to the README.

## Plans Executed

### 17-01: Update ci.yml

Added four new steps to `.github/workflows/ci.yml` after the existing `Build` step:

1. **Restore test dependencies** — `dotnet restore src/GsdOrchestrator.Tests/GsdOrchestrator.Tests.csproj`
2. **Build tests** — `dotnet build ... --no-restore --configuration Release`
3. **Test** — `dotnet test ... --configuration Release --logger trx --no-build --collect:"XPlat Code Coverage" --results-directory ./TestResults`
4. **Upload coverage** — `actions/upload-artifact@v4` (runs `if: always()`) uploads `TestResults/` as artifact `coverage-results`

The test project already had `coverlet.collector` v10.0.1 as a `PackageReference`, so no `.csproj` changes were needed.

### 17-02: Add coverage badge to README

Inserted a `![Tests](https://img.shields.io/badge/tests-35%20passing-brightgreen)` badge on the line immediately after the existing CI badge in `README.md`. Uses a static shields.io badge reflecting the current 35-test suite. No external coverage service (Codecov/Coveralls) integration required.

## Files Changed

| File | Change |
|---|---|
| `.github/workflows/ci.yml` | Added restore-tests, build-tests, test, upload-artifact steps |
| `README.md` | Added Tests badge after CI badge |

## Verification Notes

- `coverlet.collector` v10.0.1 confirmed in `GsdOrchestrator.Tests.csproj` prior to changes.
- Workflow runs on `windows-latest` matching the existing job configuration.
- `--no-build` flag on `dotnet test` relies on the explicit build-tests step above it.
- Coverage output lands in `TestResults/` which is then uploaded as a CI artifact.

## Status

Complete. Commit: `ci: add dotnet test + Coverlet coverage collection to CI workflow`
