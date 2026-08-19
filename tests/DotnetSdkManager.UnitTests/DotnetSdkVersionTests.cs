using DotnetSdkManager.Models;

namespace DotnetSdkManager.UnitTests;

public sealed class DotnetSdkVersionTests
{
    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.1.818")]
    [InlineData("10.0.100")]
    [InlineData("11.0.100-preview.2.25125.3")]
    [InlineData("2.1.300-preview1-008174")]
    public void Parse_accepts_historical_and_modern_versions(string value)
    {
        var parsed = DotnetSdkVersion.Parse(value);
        Assert.Equal(value, parsed.Original);
    }

    [Fact]
    public void Stable_version_sorts_after_preview_of_same_numeric_version()
    {
        var stable = DotnetSdkVersion.Parse("10.0.100");
        var preview = DotnetSdkVersion.Parse("10.0.100-preview.7");
        Assert.True(stable.CompareTo(preview) > 0);
    }

    [Fact]
    public void Numeric_preview_identifiers_are_ordered_numerically()
    {
        var two = DotnetSdkVersion.Parse("10.0.100-preview.2");
        var ten = DotnetSdkVersion.Parse("10.0.100-preview.10");
        Assert.True(two.CompareTo(ten) < 0);
    }

    [Fact]
    public void Channel_uses_major_and_minor()
    {
        Assert.Equal("8.0", DotnetSdkVersion.Parse("8.0.408").Channel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("10")]
    [InlineData("10.x.100")]
    public void Invalid_versions_are_rejected(string value)
    {
        Assert.False(DotnetSdkVersion.TryParse(value, out _));
    }
}
