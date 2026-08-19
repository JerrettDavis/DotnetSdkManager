using System.Text.Json.Nodes;
using DotnetSdkManager.Services;

namespace DotnetSdkManager.UnitTests;

public sealed class GlobalJsonServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"sdk-manager-global-json-{Guid.NewGuid():N}");

    public GlobalJsonServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task Legacy_sdk_removes_modern_fields_and_preserves_unrelated_content()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "global.json"),
            """
            {
              "sdk": {
                "version": "10.0.100",
                "rollForward": "latestMajor",
                "allowPrerelease": true,
                "custom": "preserve"
              },
              "msbuild-sdks": { "Example": "1.0.0" }
            }
            """);

        await new GlobalJsonService().WriteAsync(_directory, "2.1.818", "latestMajor", true);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(_directory, "global.json")))!.AsObject();
        var sdk = root["sdk"]!.AsObject();
        Assert.Equal("2.1.818", sdk["version"]!.GetValue<string>());
        Assert.False(sdk.ContainsKey("rollForward"));
        Assert.False(sdk.ContainsKey("allowPrerelease"));
        Assert.Equal("preserve", sdk["custom"]!.GetValue<string>());
        Assert.NotNull(root["msbuild-sdks"]);
    }

    [Fact]
    public async Task Modern_sdk_writes_requested_selection_fields()
    {
        await new GlobalJsonService().WriteAsync(_directory, "10.0.100", "latestFeature", false);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(_directory, "global.json")))!.AsObject();
        var sdk = root["sdk"]!.AsObject();
        Assert.Equal("latestFeature", sdk["rollForward"]!.GetValue<string>());
        Assert.False(sdk["allowPrerelease"]!.GetValue<bool>());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
