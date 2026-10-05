using System.Security.Cryptography;
using System.Text.Json;

namespace Phraseback.Tools;

public static class StudioFixture
{
    public const string RecordingId = "synthetic-settings";
    private const string Marker = ".flow-recorder-development";
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Install(string repository, string destination)
    {
        repository = Path.GetFullPath(repository);
        destination = Path.GetFullPath(destination, repository);
        ValidateDestination(repository, destination);
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any() && !File.Exists(Path.Combine(destination, Marker)))
            throw new IOException("Fixture setup refuses nonempty unmarked data. Choose a new directory beneath .tmp.");
        Directory.CreateDirectory(destination);
        ValidateDestination(repository, Path.Combine(destination, ".fixture-setup.lock"));
        ValidateDestination(repository, Path.Combine(destination, Marker));
        // Serialize development setup only; existing recording data is never modified.
        using var ownership = new FileStream(Path.Combine(destination, ".fixture-setup.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using (File.Open(Path.Combine(destination, Marker), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read)) { }
        var sessions = Path.Combine(destination, "sessions");
        var target = Path.Combine(sessions, RecordingId);
        ValidateDestination(repository, target);
        if (File.Exists(Path.Combine(target, "project.json"))) return target;
        if (Directory.Exists(target)) throw new IOException("An incomplete fixture already exists; it was left unchanged. Choose a new fixture root.");
        var stage = Path.Combine(destination, ".fixture-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            using var manifestSource = Resource("manifest.json");
            using var manifest = JsonDocument.Parse(manifestSource);
            foreach (var entry in manifest.RootElement.EnumerateObject())
            {
                if (entry.Name is not ("project.json" or "frames.jsonl") &&
                    !(entry.Name.StartsWith("frames/", StringComparison.Ordinal) && entry.Name.EndsWith(".png", StringComparison.Ordinal) && entry.Name[7..^4].All(char.IsAsciiDigit)))
                    throw new InvalidDataException("Unexpected fixture manifest path.");
                using var source = Resource(entry.Name);
                var bytes = new byte[source.Length]; source.ReadExactly(bytes);
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(entry.Value.GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Embedded fixture checksum mismatch: {entry.Name}");
                var path = Path.Combine(stage, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                file.Write(bytes); file.Flush(flushToDisk: true);
            }
            Directory.CreateDirectory(sessions);
            Publish(stage, target); // Never overwrites a competing or edited recording.
            return target;
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                ValidateDestination(repository, stage);
                Directory.Delete(stage, recursive: true);
            }
        }
    }

    private static Stream Resource(string name) => typeof(StudioFixture).Assembly.GetManifestResourceStream("studio/" + name)
        ?? throw new InvalidDataException($"Missing embedded fixture asset: {name}");

    internal static void Publish(string stage, string target, Action<string, string>? move = null, Action<int>? delay = null)
    {
        move ??= Directory.Move;
        delay ??= Thread.Sleep;
        for (var attempt = 0; ; attempt++)
        {
            try { move(stage, target); return; }
            // Windows scanners may briefly hold newly flushed files/directories.
            // Never retry over another publisher's destination; no overwrite/delete fallback.
            catch (IOException ex) when (attempt < 4 && (ex.HResult & 0xffff) is 5 or 32 or 33
                && Directory.Exists(stage) && !Directory.Exists(target) && !File.Exists(target))
            { delay(50 << attempt); }
        }
    }

    private static void ValidateDestination(string repository, string destination)
    {
        var temporaryRoot = Path.Combine(repository, ".tmp") + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(temporaryRoot, PathComparison) || destination.Length <= temporaryRoot.Length)
            throw new IOException("Development fixtures must be inside a dedicated repository .tmp subdirectory.");
        for (var path = destination; path is not null; path = Path.GetDirectoryName(path))
        {
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Fixture destination must not contain symbolic links or junctions.");
            if (path.Equals(repository, PathComparison)) break;
        }
    }
}
