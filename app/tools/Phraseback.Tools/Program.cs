using Phraseback.Tools;

try
{
    var root = Directory.GetCurrentDirectory();
    var write = false;
    string? fixtureRoot = null;
    for (var i = 0; i < args.Length; i++)
        switch (args[i])
        {
            case "--root" when i + 1 < args.Length: root = Path.GetFullPath(args[++i]); break;
            case "--write": write = true; break;
            case "--fixture-root" when i + 1 < args.Length: fixtureRoot = args[++i]; break;
            default: throw new ArgumentException("Usage: Phraseback.Tools [--root repository] [--write | --fixture-root destination]");
        }
    if (fixtureRoot is not null)
    {
        if (write) throw new ArgumentException("--write and --fixture-root cannot be combined.");
        Console.WriteLine(StudioFixture.Install(root, Path.GetFullPath(fixtureRoot, root)));
    }
    else
    {
        ContractGenerator.Check(root, write);
        Console.WriteLine("Rust and C# envelopes and command payloads match the shared schemas.");
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
