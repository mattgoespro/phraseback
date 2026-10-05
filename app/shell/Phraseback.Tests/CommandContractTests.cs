using System.Text.Json;
using System.Text.Json.Nodes;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

public sealed class CommandContractTests
{
    [Fact]
    public void SharedGoldensCoverEveryCommandAndRejectMalformedRequests()
    {
        using var schema = Load("commands.schema.json");
        using var golden = Load("commands.golden.json");
        var covered = new HashSet<string>();
        foreach (var item in golden.RootElement.GetProperty("valid").EnumerateArray())
        {
            var method = item.GetProperty("method").GetString()!;
            covered.Add(method);
            CommandPayloads.ValidateRequest(method, item.GetProperty("request"));
            CommandPayloads.ValidateResponse(method, item.GetProperty("request"), item.GetProperty("response"));
        }
        Assert.Equal(schema.RootElement.GetProperty("x-methods").EnumerateObject().Select(p => p.Name).Order(), covered.Order());
        foreach (var item in golden.RootElement.GetProperty("invalid").EnumerateArray())
            Assert.Throws<JsonException>(() => CommandPayloads.ValidateRequest(item.GetProperty("method").GetString()!, item.GetProperty("request")));
    }

    [Fact]
    public void MissingRequiredFieldsAreRejectedIncludingNullableResponseFields()
    {
        using var schema = Load("commands.schema.json");
        using var golden = Load("commands.golden.json");
        foreach (var item in golden.RootElement.GetProperty("valid").EnumerateArray())
        {
            var method = item.GetProperty("method").GetString()!;
            var contract = schema.RootElement.GetProperty("x-methods").GetProperty(method);
            foreach (var field in schema.RootElement.GetProperty("$defs").GetProperty(contract.GetProperty("request").GetString()!).GetProperty("required").EnumerateArray())
            {
                var request = JsonNode.Parse(item.GetProperty("request").GetRawText())!.AsObject();
                request.Remove(field.GetString()!);
                Assert.Throws<JsonException>(() => CommandPayloads.ValidateRequest(method, JsonSerializer.SerializeToElement(request)));
            }
            if (!schema.RootElement.GetProperty("$defs").TryGetProperty(contract.GetProperty("response").GetString()!, out var shape)) continue;
            foreach (var field in shape.GetProperty("required").EnumerateArray())
            {
                var response = JsonNode.Parse(item.GetProperty("response").GetRawText())!.AsObject();
                response.Remove(field.GetString()!);
                Assert.Throws<JsonException>(() => CommandPayloads.ValidateResponse(method, item.GetProperty("request"), JsonSerializer.SerializeToElement(response)));
            }
        }
    }

    [Fact]
    public async Task ClientRejectsMalformedCommandWithoutPoisoningConnection()
    {
        using var fixture = new Fixture();
        await using var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        await Assert.ThrowsAsync<JsonException>(() => client.RequestAsync<JsonElement>("model_remove", new { confirmed = "true" }, TestContext.Current.CancellationToken));
        var library = await client.RequestAsync<Library>("list_projects", new EmptyRequest(), TestContext.Current.CancellationToken);
        Assert.Single(library.Items);
    }

    [Fact]
    public void NestedNullEvidenceIsRejectedAndOptionalDefaultsMatchRust()
    {
        using var golden = Load("commands.golden.json");
        var item = golden.RootElement.GetProperty("valid").EnumerateArray().First(c => c.GetProperty("method").GetString() == "open_project");
        var response = JsonNode.Parse(item.GetProperty("response").GetRawText())!.AsObject();
        response["project"]!["frames"] = new JsonArray((JsonNode?)null);
        Assert.Throws<JsonException>(() => CommandPayloads.ValidateResponse("open_project", item.GetProperty("request"), JsonSerializer.SerializeToElement(response)));
        var request = JsonSerializer.Deserialize<MetadataPageRequest>("{\"recording_id\":\"golden\",\"revision\":0,\"kind\":\"frames\",\"offset\":0}", Contract.Json)!;
        Assert.Equal(128UL, request.Limit);
        Assert.False(JsonSerializer.Deserialize<HelloRequest>("{}", Contract.Json)!.Notifications);
    }

    private static JsonDocument Load(string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", file)));
}
