using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Phraseback.Client;
using Phraseback.Tools;
using Xunit;

namespace Phraseback.Tests;

public sealed class RealModelTests
{
    [Fact]
    public async Task LocalModelGeneratesThenManualEditSurvivesReopen()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("FLOW_REAL_MODEL") == "1",
            "Opt-in local inference; run scripts/rebuild_model_check.ps1.");
        var repository = Required("FLOW_REAL_MODEL_REPOSITORY");
        var source = Required("FLOW_REAL_MODEL_SOURCE");
        var root = Required("FLOW_REAL_MODEL_OUTPUT");
        var presetId = Required("FLOW_REAL_MODEL_PRESET");
        var presets = JsonSerializer.Deserialize<ModelPreset[]>(File.ReadAllText(
            Path.Combine(repository, "app/contracts/model-presets.json")), Contract.Json)!;
        var preset = presets.Single(p => p.Id == presetId);
        StudioFixture.Install(repository, root);
        var model = Path.Combine(root, "model");
        Directory.CreateDirectory(model);
        foreach (var asset in preset.Assets)
            CopyVerifiedAsset(Path.Combine(source, "model"), model, asset);

        var passed = false;
        var clock = Stopwatch.StartNew();
        double? generationSeconds = null;
        try
        {
            await using var client = await EngineClient.StartAsync(Fixture.Engine, root);
            var cancellation = TestContext.Current.CancellationToken;
            await client.RequestAsync<ModelStatus>("model_select", new { preset = presetId }, cancellation);
            var snapshot = await client.RequestSnapshotAsync("open_project", new { recording_id = StudioFixture.RecordingId }, cancellation);
            var originalManual = snapshot.Project.Steps.Where(s => s.Manual).ToArray();
            var operation = await client.RequestAsync<OperationStatus>("generate", new
            {
                recording_id = StudioFixture.RecordingId, revision = snapshot.Revision,
                step_id = "validation", force = true, replace_manual = true
            }, cancellation);
            var inference = Stopwatch.StartNew();
            while (!operation.Finished)
            {
                Assert.True(inference.Elapsed < TimeSpan.FromMinutes(15), "Inference deadline exceeded.");
                await Task.Delay(500, TestContext.Current.CancellationToken);
                operation = await client.RequestAsync<OperationStatus>("operation_status", new { }, cancellation);
            }
            generationSeconds = inference.Elapsed.TotalSeconds;
            Assert.Equal("completed", operation.State);
            Assert.Equal(1, operation.Result!.Value.GetProperty("completed").GetInt32());
            Assert.Equal(0, operation.Result.Value.GetProperty("failed").GetInt32());
            Assert.Equal("warm", (await client.RequestAsync<ModelStatus>("model_status", new { }, cancellation)).State);
            snapshot = await client.RequestSnapshotAsync("open_project", new { recording_id = StudioFixture.RecordingId }, cancellation);
            var step = snapshot.Project.Steps.Single(s => s.Id == "validation");
            Assert.Equal("generated", step.Status);
            Assert.False(step.Manual);
            Assert.False(string.IsNullOrWhiteSpace(step.Result));
            Assert.Equal(originalManual, snapshot.Project.Steps.Where(s => s.Manual).ToArray());
            const string manual = "Manual verification wording.";
            await client.RequestAsync<JsonElement>("edit_step", new
            {
                recording_id = StudioFixture.RecordingId, revision = snapshot.Revision,
                step_id = step.Id, title = step.Title, action = manual,
                result = step.Result, uncertainty = step.Uncertainty
            }, cancellation);
            snapshot = await client.RequestSnapshotAsync("open_project", new { recording_id = StudioFixture.RecordingId }, cancellation);
            var saved = snapshot.Project.Steps.Single(s => s.Id == "validation");
            Assert.Equal(manual, saved.Action);
            Assert.True(saved.Manual);
            Assert.Equal("unloaded", (await client.RequestAsync<ModelStatus>("model_release", new { }, cancellation)).State);
            foreach (var asset in preset.Assets) VerifyAsset(Path.Combine(source, "model", asset.Name), asset);
            passed = true;
        }
        finally
        {
            File.WriteAllText(Path.Combine(root, "model-check.json"), JsonSerializer.Serialize(new
            {
                passed, preset = presetId, elapsed_seconds = clock.Elapsed.TotalSeconds,
                startup_plus_inference_seconds = generationSeconds,
                engine_sha256 = Hash(Fixture.Engine),
                source_assets_unchanged = passed,
                network_isolation_verified = false
            }, Contract.Json));
        }
    }

    internal static void CopyVerifiedAsset(string source, string destination, ModelAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.Name) || asset.Name != Path.GetFileName(asset.Name)
            || asset.Name.Contains('/') || asset.Name.Contains('\\') || asset.Name.Contains(':') || asset.Name is "." or "..")
            throw new InvalidDataException("Asset name must be a single filename.");
        var input = Path.Combine(source, asset.Name);
        VerifyAsset(input, asset);
        var output = Path.Combine(destination, asset.Name);
        File.Copy(input, output, overwrite: false);
        VerifyAsset(output, asset);
    }

    private static void VerifyAsset(string path, ModelAsset asset)
    {
        if (new FileInfo(path).Length != asset.Size || !Hash(path).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Local asset does not match the pinned manifest.");
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing {name}; use the model-check launcher.");

    [Fact]
    public void AssetCopyValidatesBytesAndRefusesOverwriteOrTraversal()
    {
        using var fixture = new Fixture();
        var source = Path.Combine(fixture.Root, "source");
        var target = Path.Combine(fixture.Root, "target");
        Directory.CreateDirectory(source); Directory.CreateDirectory(target);
        var input = Path.Combine(source, "asset.bin");
        File.WriteAllText(input, "synthetic asset");
        var asset = new ModelAsset("asset.bin", "https://unused.invalid", new FileInfo(input).Length, Hash(input));
        CopyVerifiedAsset(source, target, asset);
        Assert.Equal(File.ReadAllBytes(input), File.ReadAllBytes(Path.Combine(target, asset.Name)));
        Assert.Throws<IOException>(() => CopyVerifiedAsset(source, target, asset));
        Assert.Throws<InvalidDataException>(() => CopyVerifiedAsset(source, target, asset with { Name = "../outside" }));
        Assert.Throws<InvalidDataException>(() => CopyVerifiedAsset(source, target, asset with { Sha256 = new string('0', 64) }));
        Assert.Equal("synthetic asset", File.ReadAllText(input));
    }
}
