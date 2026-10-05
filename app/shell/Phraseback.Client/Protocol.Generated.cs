// Generated from contracts/protocol.schema.json. Checked by Phraseback.Tools.
#nullable enable
using System.Text.Json;

namespace Phraseback.Client;

public sealed record RequestEnvelope(uint Protocol, long Id, string Method, JsonElement Params);
public sealed record Envelope(uint Protocol, long Id, string Session, JsonElement? Result, EngineFailure? Error);
public sealed record EngineNotification(uint Protocol, string Session, string Event, JsonElement Data);
public sealed record EngineFailure(string Code, string Message);
