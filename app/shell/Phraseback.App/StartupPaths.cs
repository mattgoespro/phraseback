namespace Phraseback.App;

public sealed record StartupPaths(string Engine, string DataRoot)
{
    public static StartupPaths Resolve(string[] arguments, string applicationDirectory, string? dataOverride, string localData)
    {
        string? Option(string name)
        {
            var index = Array.IndexOf(arguments, name);
            if (index < 0) return null;
            if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]) || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"{name} requires a path.");
            return arguments[index + 1];
        }
        return new(
            Path.GetFullPath(Option("--engine") ?? Path.Combine(applicationDirectory, "Phraseback.Engine.exe")),
            Path.GetFullPath(Option("--data-root") ?? (string.IsNullOrWhiteSpace(dataOverride) ? DefaultDataRoot(localData) : dataOverride)));
    }

    private static string DefaultDataRoot(string localData)
    {
        // Keep existing libraries in place, including their models and exclusive lock.
        var legacy = Path.Combine(localData, "FlowRecorder");
        return Directory.Exists(legacy) ? legacy : Path.Combine(localData, "Phraseback");
    }

    public static string? DataOverride(string? phraseback, string? legacy) =>
        string.IsNullOrWhiteSpace(phraseback) ? legacy : phraseback;

    // Debug-only launch policy. Release startup delegates directory creation and
    // exclusive ownership to the engine after its compatible protocol handshake.
    public void ValidateDevelopmentRoot()
    {
        if (!File.Exists(Path.Combine(DataRoot, ".flow-recorder-development")))
            throw new IOException("Debug builds require a marked development folder. Use the development launcher or specify an isolated --data-root. Your existing recordings have not been opened.");
    }
}
