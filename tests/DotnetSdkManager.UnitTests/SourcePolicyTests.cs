using DotnetSdkManager.Configuration;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.Security;

namespace DotnetSdkManager.UnitTests;

public sealed class SourcePolicyTests
{
    [Fact]
    public void Official_https_host_is_allowed()
    {
        var policy = new SourcePolicy(SourceOptions.Official);
        policy.Validate(SourceOptions.OfficialReleasesIndex);
    }

    [Fact]
    public void Unknown_host_is_rejected()
    {
        var policy = new SourcePolicy(SourceOptions.Official);
        Assert.Throws<PolicyViolationException>(() => policy.Validate(new Uri("https://example.test/archive.zip")));
    }

    [Fact]
    public void Explicit_custom_host_is_allowed_over_https()
    {
        var options = new SourceOptions(SourceOptions.OfficialReleasesIndex, ["example.test"], false);
        var policy = new SourcePolicy(options);
        policy.Validate(new Uri("https://example.test/archive.zip"));
    }

    [Fact]
    public void Http_requires_explicit_admission_even_for_allowed_host()
    {
        var options = new SourceOptions(SourceOptions.OfficialReleasesIndex, ["localhost"], false);
        var policy = new SourcePolicy(options);
        Assert.Throws<PolicyViolationException>(() => policy.Validate(new Uri("http://localhost/archive.zip")));
    }

    [Fact]
    public void Embedded_credentials_are_rejected()
    {
        var options = new SourceOptions(SourceOptions.OfficialReleasesIndex, ["example.test"], false);
        var policy = new SourcePolicy(options);
        Assert.Throws<PolicyViolationException>(() => policy.Validate(new Uri("https://user:secret@example.test/archive.zip")));
    }
}
