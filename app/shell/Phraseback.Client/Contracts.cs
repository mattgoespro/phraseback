using System.Text.Json;

namespace Phraseback.Client;

public static class Contract
{
    public const int Major = 1;
    public const int MaximumMessageBytes = 8 * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true
    };
}

// The generic page convenience wrapper is validated against the generated
// FramePage/StepPage contract before deserialization.
public sealed record MetadataPage<T>(T[] Items, long Revision, int? NextOffset);
public sealed partial record CaptureScreen
{
    public override string ToString() => $"{Name} · {Width} × {Height}";
}
public sealed partial record ModelPreset
{ public override string ToString() => Title; }
public sealed class EngineException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
