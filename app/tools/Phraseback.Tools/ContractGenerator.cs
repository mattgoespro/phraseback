using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Phraseback.Tools;

/// <summary>Auditable build-time generator for the protocol's deliberately small schema subset.</summary>
public static class ContractGenerator
{
    private static string Text(this JsonElement value, string key) => value.GetProperty(key).GetString()!;
    private static string Pascal(string name) => string.Concat(name.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    private static JsonDocument Read(string root, string path) => JsonDocument.Parse(File.ReadAllText(Path.Combine(root, path)));
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
    private static string Lines(List<string> lines) => string.Join('\n', lines);

    public static void Check(string root, bool write = false)
    {
        Validate(root);
        foreach (var (relative, expected) in Generate(root))
        {
            var path = Path.Combine(root, relative);
            if (write) File.WriteAllText(path, expected, new UTF8Encoding(false));
            Require(File.ReadAllText(path).Replace("\r\n", "\n") == expected, $"Protocol definition differs from the shared schema: {relative}");
        }
    }

    public static Dictionary<string, string> Generate(string root)
    {
        using var envelopes = Read(root, "app/contracts/protocol.schema.json");
        var rust = new List<string> { "// Generated from contracts/protocol.schema.json. Checked by Phraseback.Tools.", "" };
        var cs = new List<string> { rust[0], "#nullable enable", "using System.Text.Json;", "", "namespace Phraseback.Client;", "" };
        var names = new Dictionary<string, string> { ["Request"] = "RequestEnvelope", ["Response"] = "Envelope", ["Error"] = "EngineFailure", ["Notification"] = "EngineNotification" };
        foreach (var shape in envelopes.RootElement.GetProperty("$defs").EnumerateObject())
        {
            rust.AddRange(["#[derive(Debug, Deserialize, Serialize)]", "#[serde(deny_unknown_fields)]", $"pub struct {shape.Name} {{"]);
            var parameters = new List<string>();
            foreach (var field in shape.Value.GetProperty("properties").EnumerateObject())
            {
                rust.Add($"    pub {field.Name}: {field.Value.Text("x-rust")},");
                parameters.Add($"{field.Value.Text("x-csharp")} {Pascal(field.Name)}");
            }
            rust.AddRange(["}", ""]);
            cs.Add($"public sealed record {names[shape.Name]}({string.Join(", ", parameters)});");
        }
        var output = new Dictionary<string, string>
        {
            ["app/crates/flow-core/src/protocol_generated.rs"] = Lines(rust),
            ["app/shell/Phraseback.Client/Protocol.Generated.cs"] = Lines(cs) + "\n"
        };
        using var commands = Read(root, "app/contracts/commands.schema.json");
        var schema = commands.RootElement;
        rust = ["// Generated from contracts/commands.schema.json. Do not edit by hand.", "use serde::{Deserialize, Serialize};", "use serde_json::Value;", ""];
        cs = [rust[0], "#nullable enable", "using System.Text.Json;", "using System.Text.Json.Serialization;", "", "namespace Phraseback.Client;", ""];
        foreach (var shape in schema.GetProperty("$defs").EnumerateObject())
        {
            rust.AddRange(["#[derive(Debug, Deserialize, Serialize)]", "#[serde(deny_unknown_fields)]", $"pub struct {shape.Name} {{"]);
            var parameters = new List<string>();
            var helpers = new List<string>();
            var required = shape.Value.GetProperty("required").EnumerateArray().Select(v => v.GetString()).ToHashSet();
            foreach (var field in shape.Value.GetProperty("properties").EnumerateObject())
            {
                var optional = !required.Contains(field.Name);
                var rustType = field.Value.Text("x-rust");
                if (optional)
                {
                    var defaultValue = field.Value.GetProperty("default").GetRawText();
                    if (rustType == "String") defaultValue += ".into()";
                    if (defaultValue is not ("0" or "false" or "null"))
                    {
                        var helper = $"default_{shape.Name.ToLowerInvariant()}_{field.Name}";
                        rust.Add($"    #[serde(default = \"{helper}\")]");
                        helpers.AddRange([$"fn {helper}() -> {rustType} {{", $"    {defaultValue}", "}", ""]);
                    }
                    else rust.Add("    #[serde(default)]");
                }
                else if (rustType.StartsWith("Option<", StringComparison.Ordinal)) rust.Add("    #[serde(deserialize_with = \"required_nullable\")]");
                rust.Add($"    pub {field.Name}: {rustType},");
                var attribute = optional ? "" : "[property: JsonRequired] ";
                var suffix = optional ? " = " + field.Value.GetProperty("default").GetRawText() : "";
                parameters.Add($"{attribute}{field.Value.Text("x-csharp")} {Pascal(field.Name)}{suffix}");
            }
            rust.AddRange(["}", ""]); rust.AddRange(helpers);
            cs.AddRange(["[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]",
                $"public sealed partial record {shape.Name}({string.Join(", ", parameters.OrderBy(p => p.Contains(" = ", StringComparison.Ordinal)))});", ""]);
        }
        rust.AddRange(["fn required_nullable<'de, D, T>(deserializer: D) -> Result<Option<T>, D::Error>", "where", "    D: serde::Deserializer<'de>,", "    T: Deserialize<'de>,", "{", "    Option::<T>::deserialize(deserializer)", "}", "",
            "fn check<T: serde::de::DeserializeOwned>(value: &Value) -> Result<(), String> {", "    serde_json::from_value::<T>(value.clone())", "        .map(|_| ())", "        .map_err(|_| \"Invalid command payload\".into())", "}", "",
            "#[rustfmt::skip]", "pub fn validate_request(method: &str, value: &Value) -> Result<(), String> {", "    match method {"]);
        cs.AddRange(["public static class CommandPayloads", "{", "    private static void Check<T>(JsonElement value)", "    {", "        if (value.ValueKind == JsonValueKind.Null) throw new JsonException(\"Null command payload\");",
            "        CheckElements(value.Deserialize<T>(Contract.Json));", "    }", "    private static void CheckElements(object? value)", "    {", "        if (value is null) throw new JsonException(\"Null required payload element\");",
            "        switch (value)", "        {", "            case Array array: foreach (var item in array) CheckElements(item); break;"]);
        foreach (var shape in schema.GetProperty("$defs").EnumerateObject())
        {
            var children = shape.Value.GetProperty("properties").EnumerateObject().Where(p => p.Value.TryGetProperty("$ref", out _) || p.Value.TryGetProperty("type", out var type) && type.GetString() == "array").ToArray();
            if (children.Length > 0) cs.Add($"            case {shape.Name} item: {string.Join(" ", children.Select(p => $"CheckElements(item.{Pascal(p.Name)});"))} break;");
        }
        cs.AddRange(["        }", "    }", "    public static void ValidateRequest(string method, JsonElement value)", "    {", "        switch (method)", "        {"]);
        foreach (var method in schema.GetProperty("x-methods").EnumerateObject())
        {
            rust.Add($"        \"{method.Name}\" => check::<{method.Value.Text("request")}>(value),");
            cs.Add($"            case \"{method.Name}\": Check<{method.Value.Text("request")}>(value); break;");
        }
        rust.AddRange(["        _ => Err(\"Unknown command\".into()),", "    }", "}", "", "#[rustfmt::skip]", "pub fn validate_response(method: &str, request: &Value, value: &Value) -> Result<(), String> {", "    match method {"]);
        cs.AddRange(["            default: throw new JsonException(\"Unknown command\");", "        }", "    }", "", "    public static void ValidateResponse(string method, JsonElement request, JsonElement value)", "    {", "        switch (method)", "        {"]);
        foreach (var method in schema.GetProperty("x-methods").EnumerateObject())
        {
            var response = method.Value.Text("response");
            if (response == "MetadataPage")
            {
                rust.Add("        \"metadata_page\" => if request[\"kind\"] == \"frames\" { check::<FramePage>(value) } else { check::<StepPage>(value) },");
                cs.Add("            case \"metadata_page\": if (request.GetProperty(\"kind\").GetString() == \"frames\") Check<FramePage>(value); else Check<StepPage>(value); break;");
            }
            else
            {
                rust.Add($"        \"{method.Name}\" => check::<{response}>(value),");
                var csType = response.Replace("Vec<", "").Replace("Option<", "").Replace(">", "") + (response.StartsWith("Vec<", StringComparison.Ordinal) ? "[]" : "");
                var guard = response.StartsWith("Option<", StringComparison.Ordinal) ? "if (value.ValueKind != JsonValueKind.Null) " : "";
                cs.Add($"            case \"{method.Name}\": {guard}Check<{csType}>(value); break;");
            }
        }
        rust.AddRange(["        _ => Err(\"Unknown command\".into()),", "    }", "}", ""]);
        cs.AddRange(["            default: throw new JsonException(\"Unknown command\");", "        }", "    }", "}", ""]);
        output["app/crates/flow-core/src/commands_generated.rs"] = Lines(rust);
        output["app/shell/Phraseback.Client/Commands.Generated.cs"] = Lines(cs);
        return output;
    }

    public static void Validate(string root)
    {
        using var document = Read(root, "app/contracts/commands.schema.json");
        using var golden = Read(root, "app/contracts/commands.golden.json");
        var schema = document.RootElement;
        var definitions = schema.GetProperty("$defs");
        var methods = schema.GetProperty("x-methods");
        void ValidateValue(JsonElement value, JsonElement shape)
        {
            if (shape.TryGetProperty("$ref", out var reference)) { ValidateValue(value, definitions.GetProperty(reference.GetString()!.Split('/')[^1])); return; }
            if (shape.TryGetProperty("anyOf", out var choices))
            {
                foreach (var choice in choices.EnumerateArray())
                    try { ValidateValue(value, choice); return; } catch (InvalidDataException) { }
                throw new InvalidDataException("No matching nullable payload type");
            }
            var kind = shape.Text("type");
            Require(kind switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "integer" => value.ValueKind == JsonValueKind.Number && (value.TryGetInt64(out _) || value.TryGetUInt64(out _)),
                "null" => value.ValueKind == JsonValueKind.Null,
                _ => throw new InvalidDataException($"Unsupported schema type: {kind}")
            }, $"Expected {kind}");
            if (kind == "object" && shape.TryGetProperty("properties", out var properties))
            {
                foreach (var required in shape.GetProperty("required").EnumerateArray()) Require(value.TryGetProperty(required.GetString()!, out _), $"Missing required field: {required}");
                foreach (var field in value.EnumerateObject())
                {
                    Require(properties.TryGetProperty(field.Name, out var child), $"Unknown field: {field.Name}");
                    ValidateValue(field.Value, child);
                }
            }
            else if (kind == "array") foreach (var item in value.EnumerateArray()) ValidateValue(item, shape.GetProperty("items"));
            else if (kind == "integer")
            {
                var number = value.GetDecimal();
                Require(!shape.TryGetProperty("minimum", out var minimum) || number >= minimum.GetDecimal(), "Integer below minimum");
                Require(!shape.TryGetProperty("maximum", out var maximum) || number <= maximum.GetDecimal(), "Integer above maximum");
            }
        }
        var covered = new HashSet<string>();
        foreach (var example in golden.RootElement.GetProperty("valid").EnumerateArray())
        {
            var name = example.Text("method"); covered.Add(name);
            var method = methods.GetProperty(name);
            ValidateValue(example.GetProperty("request"), definitions.GetProperty(method.Text("request")));
            var response = method.Text("response");
            if (response == "MetadataPage") response = example.GetProperty("request").Text("kind") == "frames" ? "FramePage" : "StepPage";
            if (response.StartsWith("Option<", StringComparison.Ordinal))
            {
                if (example.GetProperty("response").ValueKind == JsonValueKind.Null) continue;
                response = response[7..^1];
            }
            if (response.StartsWith("Vec<", StringComparison.Ordinal))
            {
                Require(example.GetProperty("response").ValueKind == JsonValueKind.Array, "Expected response array");
                foreach (var item in example.GetProperty("response").EnumerateArray()) ValidateValue(item, definitions.GetProperty(response[4..^1]));
            }
            else ValidateValue(example.GetProperty("response"), definitions.GetProperty(response));
        }
        Require(covered.SetEquals(methods.EnumerateObject().Select(p => p.Name)), "Missing command golden");
        var engine = File.ReadAllText(Path.Combine(root, "app/crates/flow-engine/src/main.rs"));
        var implemented = new HashSet<string> { "hello" };
        foreach (Match arm in Regex.Matches(engine, "^ {12}((?:\"[a-z_]+\"\\s*\\|\\s*)*\"[a-z_]+\")\\s*=>", RegexOptions.Multiline))
            foreach (Match name in Regex.Matches(arm.Groups[1].Value, "\"([a-z_]+)\"")) implemented.Add(name.Groups[1].Value);
        Require(implemented.SetEquals(methods.EnumerateObject().Select(p => p.Name)), "Engine command surface differs from schema");
    }
}
