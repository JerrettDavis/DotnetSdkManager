using System.Net;
using DotnetSdkManager.Configuration;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.Http;
using DotnetSdkManager.Security;

namespace DotnetSdkManager.UnitTests;

public sealed class ValidatedHttpClientTests
{
    [Fact]
    public async Task Redirect_target_is_revalidated()
    {
        var source = new SourceOptions(SourceOptions.OfficialReleasesIndex, ["allowed.test"], false);
        using var client = new ValidatedHttpClient(new SourcePolicy(source), new RedirectHandler());

        await Assert.ThrowsAsync<PolicyViolationException>(
            () => client.GetAsync(new Uri("https://allowed.test/start")));
    }

    private sealed class RedirectHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("https://not-allowed.test/archive.zip") }
            };
            return Task.FromResult(response);
        }
    }
}
