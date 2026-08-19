using DotnetSdkManager.IntegrationTests.Support;

namespace DotnetSdkManager.IntegrationTests;

public sealed class MetadataCacheTests
{
    [Fact]
    public async Task Uses_conditional_requests_and_can_reuse_cache_offline()
    {
        using var temp = new TemporaryDirectory("sdk-manager-cache");
        await using var feed = await LoopbackFeed.CreateAsync(temp.Path);
        using var services = TestServices.Create(Path.Combine(temp.Path, "home"), feed);

        var first = await services.Catalog.GetChannelsAsync(offline: false);
        var second = await services.Catalog.GetChannelsAsync(offline: false);
        var offline = await services.Catalog.GetChannelsAsync(offline: true);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Single(offline);
        Assert.True(feed.NotModifiedCount >= 1);
    }
}
