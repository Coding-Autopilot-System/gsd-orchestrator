using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GsdOrchestrator.Workflows.Models;
using Xunit;

namespace GsdOrchestrator.Tests;

/// <summary>
/// Consumer-side contract-compatibility gate against the pinned cas-contracts schema
/// version. gsd-orchestrator produces the CAS v1.1 SdlcProfile contract via
/// <see cref="SdlcProfile.CasSdlcV1"/>; these tests fail red in CI when that in-code
/// profile drifts from the vendored cas-contracts v1.1 schema.
///
/// The invariants asserted here are read out of the vendored schema file itself
/// (required fields, enums, consts, phase count), so an upstream schema change is
/// what makes the check react — rather than re-hardcoding the rules. This keeps the
/// check self-contained and offline (no JSON-Schema validator NuGet dependency).
///
/// Cross-repo reference note: cas-contracts is a separate repo. In isolated CI only
/// this repo is checked out, so the pinned v1.1.0 release is vendored under
/// GsdOrchestrator.Tests/Contracts/cas-contracts/v1.1.0/ (same convention as the
/// Python consumers). When the sibling ../cas-contracts checkout is present
/// (local dev), an extra test asserts the vendored copy has not drifted upstream.
/// </summary>
public class ContractCompatibilityTests
{
    // The cas-contracts version this consumer targets. Kept in lockstep with the
    // vendored release and with SdlcProfile.CasSdlcV1.ProfileVersion.
    private const string PinnedSchemaVersion = "1.1.0";
    private const string PinnedProfileVersion = "v1.1";

    private static string ContractRoot { get; } = Path.Combine(
        AppContext.BaseDirectory, "Contracts", "cas-contracts", $"v{PinnedSchemaVersion}");

    private static JsonNode LoadSchema(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(ContractRoot, name)))!;

    private static byte[] ReadCanonicalUtf8Bytes(string path)
    {
        var text = File.ReadAllText(path);
        var normalized = text.ReplaceLineEndings("\r\n");
        return Encoding.UTF8.GetBytes(normalized);
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Walk up from the test output directory to find a sibling
    /// cas-contracts/registry/releases/v{version} checkout, or null if absent.</summary>
    private static string? ResolveUpstreamRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "cas-contracts", "registry", "releases", $"v{PinnedSchemaVersion}");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>Serialize CasSdlcV1 into the camelCase v1.1 wire shape, including the
    /// mandatory lifecycle metadata that every v1.1 contract requires.</summary>
    private static JsonObject BuildProfileWirePayload()
    {
        var profile = SdlcProfile.CasSdlcV1;
        var phases = new JsonArray();
        foreach (var phase in profile.Phases)
        {
            var phaseNode = new JsonObject
            {
                ["id"] = phase.Id,
                ["name"] = phase.Name,
                ["batch"] = phase.Batch.ToString().ToLowerInvariant(),
                ["dependencies"] = new JsonArray(phase.Dependencies.Select(d => (JsonNode)d!).ToArray()),
                ["verifier"] = phase.Verifier,
                ["requiredArtifacts"] = new JsonArray(phase.RequiredArtifacts.Select(a => (JsonNode)a!).ToArray()),
                ["successCriteria"] = new JsonArray(phase.SuccessCriteria.Select(c => (JsonNode)c!).ToArray()),
                ["maxAttempts"] = phase.MaxAttempts,
            };
            if (phase.RollbackTo is not null)
            {
                phaseNode["rollbackTo"] = phase.RollbackTo;
            }

            phases.Add(phaseNode);
        }

        return new JsonObject
        {
            // lifecycleMetadata (common.schema.json#/$defs/lifecycleMetadata)
            ["correlationId"] = "corr-001",
            ["promptId"] = "prompt-001",
            ["runId"] = "run-001",
            ["repo"] = "Coding-Autopilot-System/gsd-orchestrator",
            ["actor"] = new JsonObject { ["id"] = "gsd-orchestrator", ["type"] = "service" },
            ["timestamp"] = "2026-07-03T00:00:00Z",
            ["schemaVersion"] = PinnedSchemaVersion,
            ["traceContext"] = new JsonObject
            {
                ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            },
            // SdlcProfile body
            ["kind"] = "SdlcProfile",
            ["profileId"] = profile.ProfileId,
            ["profileVersion"] = profile.ProfileVersion,
            ["goalBudget"] = profile.GoalBudget,
            ["phases"] = phases,
            ["goalAttempts"] = profile.GoalAttempts,
            ["iterations"] = profile.Iterations,
            ["runtimeMinutes"] = profile.RuntimeMinutes,
            ["modelCalls"] = profile.ModelCalls,
            ["noProgressLimit"] = profile.NoProgressLimit,
        };
    }

    [Fact]
    public void VendoredRelease_MatchesManifestHashes()
    {
        var manifest = LoadSchema("manifest.json");
        Assert.Equal(PinnedSchemaVersion, (string?)manifest["version"]);

        foreach (var entry in manifest["schemas"]!.AsArray())
        {
            var path = (string)entry!["path"]!;
            var expected = (string)entry["sha256"]!;
            var bytes = ReadCanonicalUtf8Bytes(Path.Combine(ContractRoot, path));
            var actual = Sha256Hex(bytes);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void CanonicalSchemaDigest_IgnoresLineEndings()
    {
        var crlfPath = Path.GetTempFileName();
        var lfPath = Path.GetTempFileName();

        try
        {
            File.WriteAllText(crlfPath, "{\r\n  \"name\": \"schema\"\r\n}\r\n", new UTF8Encoding(false));
            File.WriteAllText(lfPath, "{\n  \"name\": \"schema\"\n}\n", new UTF8Encoding(false));

            var crlf = Sha256Hex(ReadCanonicalUtf8Bytes(crlfPath));
            var lf = Sha256Hex(ReadCanonicalUtf8Bytes(lfPath));

            Assert.Equal(crlf, lf);
        }
        finally
        {
            File.Delete(crlfPath);
            File.Delete(lfPath);
        }
    }

    [Fact]
    public void PinnedVersion_IsTheVersionThisConsumerEmits()
    {
        // schemaVersion const enforced by the vendored schema must equal the pin...
        var common = LoadSchema("common.schema.json");
        var schemaConst = (string?)common["$defs"]!["lifecycleMetadata"]!["properties"]!
            ["schemaVersion"]!["const"];
        Assert.Equal(PinnedSchemaVersion, schemaConst);

        // ...and the in-code profile must emit the pinned profileVersion.
        Assert.Equal(PinnedProfileVersion, SdlcProfile.CasSdlcV1.ProfileVersion);

        // The vendored profile schema pins the same profileVersion via a regex pattern.
        var profileSchema = LoadSchema("sdlc-profile.schema.json");
        var pattern = (string?)profileSchema["allOf"]![1]!["properties"]!
            ["profileVersion"]!["pattern"];
        Assert.Equal("^v1\\.1$", pattern);
    }

    [Fact]
    public void CasSdlcV1_ConformsToVendoredProfileSchema()
    {
        var payload = BuildProfileWirePayload();
        var schema = LoadSchema("sdlc-profile.schema.json");
        var body = schema["allOf"]![1]!;
        var props = body["properties"]!;

        // Required top-level SdlcProfile fields are present.
        foreach (var required in body["required"]!.AsArray())
        {
            Assert.True(payload.ContainsKey((string)required!),
                $"payload missing required field '{required}'");
        }

        // profileVersion matches the schema const, and kind matches.
        Assert.Equal("SdlcProfile", (string?)payload["kind"]);
        Assert.Equal(PinnedProfileVersion, (string?)payload["profileVersion"]);

        // Phase count matches the schema bounds (v1.1: exactly 12 phases).
        var minItems = (int)props["phases"]!["minItems"]!;
        var maxItems = (int)props["phases"]!["maxItems"]!;
        var phases = payload["phases"]!.AsArray();
        Assert.InRange(phases.Count, minItems, maxItems);

        // Every phase id / batch value is a member of the schema enums.
        var common = LoadSchema("common.schema.json");
        var phaseIdEnum = common["$defs"]!["phaseId"]!["enum"]!.AsArray()
            .Select(n => (string)n!).ToHashSet();
        var batchEnum = common["$defs"]!["executionBatch"]!["enum"]!.AsArray()
            .Select(n => (string)n!).ToHashSet();

        foreach (var phase in phases)
        {
            var id = (string)phase!["id"]!;
            var batch = (string)phase["batch"]!;
            Assert.Contains(id, phaseIdEnum);
            Assert.Contains(batch, batchEnum);
            Assert.False(string.IsNullOrWhiteSpace((string?)phase["verifier"]));
        }
    }

    [Fact]
    public void WorkflowContextSchemaVersion_TracksPinnedMajorMinor()
    {
        // GsdWorkflowContext.SchemaVersion is "1.1"; guard it stays aligned with the
        // pinned contract major.minor so checkpoints and contracts do not diverge.
        var context = new GsdWorkflowContext();
        var pinnedMajorMinor = string.Join('.', PinnedSchemaVersion.Split('.').Take(2));
        Assert.Equal(pinnedMajorMinor, context.SchemaVersion);
    }

    [Fact]
    public void VendoredRelease_MatchesUpstreamSourceOfTruth()
    {
        // Local-only drift guard against the sibling cas-contracts repo. In isolated CI
        // the sibling is not checked out, so this test passes vacuously (no NuGet skip
        // dependency). Locally it fails red if the vendored copy drifts from upstream.
        var upstreamRoot = ResolveUpstreamRoot();
        if (upstreamRoot is null)
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(ContractRoot, "*.json"))
        {
            var upstream = Path.Combine(upstreamRoot, Path.GetFileName(file));
            Assert.True(File.Exists(upstream), $"upstream missing {Path.GetFileName(file)}");
            var local = Sha256Hex(ReadCanonicalUtf8Bytes(file));
            var remote = Sha256Hex(ReadCanonicalUtf8Bytes(upstream));
            Assert.Equal(remote, local);
        }
    }
}
