using System.Security.Cryptography;
using System.Text.Json;
using Phraseback.Client;
using Phraseback.Tools;
using Xunit;

namespace Phraseback.Tests;

public sealed class StudioFixtureTests
{
    [Fact]
    public async Task EmbeddedFixtureOpensInEngineWithEveryOriginalReviewState()
    {
        using var scratch = new Scratch();
        var directory = StudioFixture.Install(scratch.Repository, scratch.Data);
        var before = Hashes(directory);
        await using (var client = await EngineClient.StartAsync(Fixture.Engine, scratch.Data))
        {
            var snapshot = await client.RequestSnapshotAsync("open_project", new { recording_id = StudioFixture.RecordingId }, TestContext.Current.CancellationToken);
            Assert.Equal(5, snapshot.FrameCount);
            Assert.Equal(5, snapshot.StepCount);
            Assert.Equal(1440, snapshot.Project.Width);
            Assert.Equal(900, snapshot.Project.Height);
            Assert.Equal(8500, snapshot.Project.DurationMs);
            Assert.Equal(new[] { "intro", "appearance", "validation", "rename", "saved" }, snapshot.Project.Steps.Select(s => s.Id));
            Assert.Equal(new[] { "generated", "generated", "pending", "stale", "edited" }, snapshot.Project.Steps.Select(s => s.Status));
            Assert.True(snapshot.Project.Steps[0].Reviewed);
            Assert.True(snapshot.Project.Steps[3].Manual);
            Assert.True(snapshot.Project.Steps[4].Manual);
        }
        Assert.Equal(before, Hashes(directory));
    }

    [Fact]
    public void SetupPreservesEveryByteOfAnEditedExistingFixture()
    {
        using var scratch = new Scratch();
        var directory = StudioFixture.Install(scratch.Repository, scratch.Data);
        File.AppendAllText(Path.Combine(directory, "project.json"), "\n ");
        File.WriteAllText(Path.Combine(directory, "manual-note.txt"), "Keep this user addition");
        var before = Hashes(directory);
        Assert.Equal(directory, StudioFixture.Install(scratch.Repository, scratch.Data));
        Assert.Equal(before, Hashes(directory));
    }

    [Fact]
    public void SetupRefusesUnmarkedDataAndIncompleteExistingProjects()
    {
        using var scratch = new Scratch();
        Directory.CreateDirectory(scratch.Data);
        var sentinel = Path.Combine(scratch.Data, "keep.txt"); File.WriteAllText(sentinel, "Existing data");
        Assert.Throws<IOException>(() => StudioFixture.Install(scratch.Repository, scratch.Data));
        Assert.Equal("Existing data", File.ReadAllText(sentinel));
        Assert.False(File.Exists(Path.Combine(scratch.Data, ".flow-recorder-development")));
        File.WriteAllText(Path.Combine(scratch.Data, ".flow-recorder-development"), "");
        var target = Path.Combine(scratch.Data, "sessions", StudioFixture.RecordingId); Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "frames.jsonl"), "partial existing evidence");
        Assert.Throws<IOException>(() => StudioFixture.Install(scratch.Repository, scratch.Data));
        Assert.Equal("partial existing evidence", File.ReadAllText(Path.Combine(target, "frames.jsonl")));
        Assert.Empty(Directory.GetDirectories(scratch.Data, ".fixture-stage-*"));
    }

    [Theory]
    [InlineData(".")]
    [InlineData(".tmp")]
    [InlineData(".tmp/../normal-data")]
    [InlineData(".tmp-other/recordings")]
    public void SetupRejectsRootsOutsideDedicatedTemporarySubfolders(string destination)
    {
        using var scratch = new Scratch();
        Assert.Throws<IOException>(() => StudioFixture.Install(scratch.Repository, Path.Combine(scratch.Repository, destination)));
        Assert.Empty(Directory.GetFileSystemEntries(scratch.Repository));
    }

    [Fact]
    public void PublicationRetriesTransientSharingErrorsWithoutReplacingDestinations()
    {
        using var scratch = new Scratch();
        var stage = Path.Combine(scratch.Repository, "stage"); Directory.CreateDirectory(stage);
        var target = Path.Combine(scratch.Repository, "target");
        File.WriteAllText(Path.Combine(stage, "evidence"), "original bytes");
        var attempts = 0; var delays = new List<int>();
        StudioFixture.Publish(stage, target, (from, to) =>
        {
            if (++attempts < 3) throw new IOException("Synthetic sharing violation", unchecked((int)0x80070020));
            Directory.Move(from, to);
        }, delays.Add);
        Assert.Equal(3, attempts); Assert.Equal(new[] { 50, 100 }, delays);
        Assert.Equal("original bytes", File.ReadAllText(Path.Combine(target, "evidence")));
        Directory.CreateDirectory(stage); attempts = 0;
        Assert.Throws<IOException>(() => StudioFixture.Publish(stage, target, (_, _) =>
        {
            attempts++; throw new IOException("Synthetic access denied", unchecked((int)0x80070005));
        }, _ => Assert.Fail("Must not retry over an existing destination")));
        Assert.Equal(1, attempts);
        Assert.Equal("original bytes", File.ReadAllText(Path.Combine(target, "evidence")));
    }

    [Fact]
    public void PublicationStopsAfterBoundedRetriesAndPreservesStagedEvidence()
    {
        using var scratch = new Scratch();
        var stage = Path.Combine(scratch.Repository, "stage"); Directory.CreateDirectory(stage);
        var target = Path.Combine(scratch.Repository, "target");
        var attempts = 0; var delays = new List<int>();
        Assert.Throws<IOException>(() => StudioFixture.Publish(stage, target, (_, _) =>
        {
            attempts++; throw new IOException("Synthetic persistent sharing violation", unchecked((int)0x80070020));
        }, delays.Add));
        Assert.Equal(5, attempts); Assert.Equal(750, delays.Sum());
        Assert.True(Directory.Exists(stage)); Assert.False(Directory.Exists(target));
    }

    private static string[] Hashes(string directory) => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
        .Select(p => Path.GetRelativePath(directory, p) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))
        .Order(StringComparer.Ordinal).ToArray();

    private sealed class Scratch : IDisposable
    {
        public string Repository { get; } = Path.Combine(Environment.GetEnvironmentVariable("FLOW_REBUILD_TEST_ROOT") ?? Path.Combine(Directory.GetCurrentDirectory(), ".tmp/fixture-tests"), Guid.NewGuid().ToString("N"));
        public string Data => Path.Combine(Repository, ".tmp", "isolated-data");
        public Scratch() => Directory.CreateDirectory(Repository);
        public void Dispose() => Directory.Delete(Repository, recursive: true);
    }
}
