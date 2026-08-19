using DotnetSdkManager.Services;

namespace DotnetSdkManager.UnitTests;

public sealed class InventoryServiceTests
{
    [Fact]
    public void Parses_dotnet_list_sdks_output()
    {
        const string output = """
            8.0.408 [/usr/share/dotnet/sdk]
            10.0.100 [C:\Program Files\dotnet\sdk]
            malformed
            """;

        var result = InventoryService.ParseSystemSdkList(output);
        Assert.Equal(2, result.Count);
        Assert.Equal("8.0.408", result[0].Version);
        Assert.Equal("/usr/share/dotnet/sdk", result[0].Root);
    }
}
