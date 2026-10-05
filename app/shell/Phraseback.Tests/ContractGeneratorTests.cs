using System.Text.Json.Nodes;
using Phraseback.Tools;
using Xunit;

namespace Phraseback.Tests;

public sealed class ContractGeneratorTests
{
    [Fact]
    public void GeneratorMatchesCheckedInContractsAndWriteRepairsDrift()
    {
        using var fixture = new GeneratorFixture();
        ContractGenerator.Check(fixture.Root);
        Assert.Equal(4, ContractGenerator.Generate(fixture.Root).Count);
        var output = Path.Combine(fixture.Root, "app/shell/Phraseback.Client/Commands.Generated.cs");
        File.AppendAllText(output, "// accidental change\n");
        Assert.Throws<InvalidDataException>(() => ContractGenerator.Check(fixture.Root));
        ContractGenerator.Check(fixture.Root, write: true);
        ContractGenerator.Check(fixture.Root);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("wrong-type")]
    [InlineData("missing-command")]
    public void GeneratorRejectsInvalidGoldenBeforeWriting(string mutation)
    {
        using var fixture = new GeneratorFixture();
        var golden = JsonNode.Parse(File.ReadAllText(fixture.Golden))!.AsObject();
        var cases = golden["valid"]!.AsArray();
        var open = cases.Select(n => n!.AsObject()).First(n => n["method"]!.GetValue<string>() == "open_project");
        var request = open["request"]!.AsObject();
        switch (mutation)
        {
            case "missing": request.Remove(request.First().Key); break;
            case "unknown": request["unknown"] = 1; break;
            case "wrong-type": request[request.First().Key] = new JsonArray(); break;
            case "missing-command":
                foreach (var item in cases.Where(n => n!["method"]!.GetValue<string>() == "hello").ToArray()) cases.Remove(item);
                break;
        }
        File.WriteAllText(fixture.Golden, golden.ToJsonString());
        var before = ContractGenerator.Generate(fixture.Root).Keys.ToDictionary(p => p, p => File.ReadAllBytes(Path.Combine(fixture.Root, p)));
        Assert.Throws<InvalidDataException>(() => ContractGenerator.Check(fixture.Root, write: true));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(fixture.Root, path)));
    }

    [Fact]
    public void GeneratorRejectsUnlistedEngineCommands()
    {
        using var fixture = new GeneratorFixture();
        File.AppendAllText(Path.Combine(fixture.Root, "app/crates/flow-engine/src/main.rs"), "\n            \"undocumented\" => {}\n");
        Assert.Throws<InvalidDataException>(() => ContractGenerator.Check(fixture.Root));
    }

    private sealed class GeneratorFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Environment.GetEnvironmentVariable("FLOW_REBUILD_TEST_ROOT") ?? Path.Combine(Directory.GetCurrentDirectory(), ".tmp/generator-tests"), Guid.NewGuid().ToString("N"));
        public string Golden => Path.Combine(Root, "app/contracts/commands.golden.json");
        public GeneratorFixture()
        {
            var baseline = Path.Combine(AppContext.BaseDirectory, "generator-baseline");
            foreach (var source in Directory.EnumerateFiles(baseline, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Root, Path.GetRelativePath(baseline, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            var contracts = Path.Combine(Root, "app/contracts"); Directory.CreateDirectory(contracts);
            foreach (var source in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "contracts"))) File.Copy(source, Path.Combine(contracts, Path.GetFileName(source)));
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
