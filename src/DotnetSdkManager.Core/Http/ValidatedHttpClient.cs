using System.Net;
using System.Net.Http.Headers;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.Security;

namespace DotnetSdkManager.Http;

public sealed class ValidatedHttpClient : IDisposable
{
    private const int MaximumRedirects = 8;
    private readonly HttpClient _client;
    private readonly SourcePolicy _policy;

    public ValidatedHttpClient(SourcePolicy policy, HttpMessageHandler? handler = null)
    {
        _policy = policy;
        handler ??= new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseProxy = true
        };
        _client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DotnetSdkManager", "0.1.0"));
    }

    public async Task<HttpResponseMessage> GetAsync(
        Uri uri,
        Action<HttpRequestHeaders>? configureHeaders = null,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead,
        CancellationToken cancellationToken = default)
    {
        var current = uri;
        for (var redirect = 0; redirect <= MaximumRedirects; redirect++)
        {
            _policy.Validate(current);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            configureHeaders?.Invoke(request.Headers);
            var response = await _client.SendAsync(request, completionOption, cancellationToken);

            if (!IsRedirect(response.StatusCode))
            {
                return response;
            }

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null)
            {
                throw new PolicyViolationException($"Redirect response from '{current}' did not contain a Location header.");
            }

            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }

        throw new PolicyViolationException($"Source '{uri}' exceeded the maximum redirect count of {MaximumRedirects}.");
    }

    public void Dispose() => _client.Dispose();

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Found or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;
}
