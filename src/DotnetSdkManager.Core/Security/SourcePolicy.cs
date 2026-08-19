using DotnetSdkManager.Configuration;
using DotnetSdkManager.Exceptions;

namespace DotnetSdkManager.Security;

public sealed class SourcePolicy
{
    private static readonly string[] OfficialHosts =
    [
        "dotnetcli.blob.core.windows.net",
        "builds.dotnet.microsoft.com",
        "download.visualstudio.microsoft.com",
        "dotnetcli.azureedge.net",
        "download.microsoft.com",
        "go.microsoft.com",
        "aka.ms"
    ];

    private readonly HashSet<string> _allowedHosts;

    public SourcePolicy(SourceOptions options)
    {
        AllowHttp = options.AllowHttp;
        _allowedHosts = new HashSet<string>(OfficialHosts, StringComparer.OrdinalIgnoreCase);
        foreach (var host in options.AdditionalAllowedHosts)
        {
            var normalized = NormalizeHost(host);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _allowedHosts.Add(normalized);
            }
        }
    }

    public bool AllowHttp { get; }

    public IReadOnlyCollection<string> AllowedHosts => _allowedHosts;

    public void Validate(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            throw new PolicyViolationException($"Source URI '{uri}' must be absolute.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new PolicyViolationException($"Source URI '{uri}' must not contain embedded credentials.");
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new PolicyViolationException($"Source URI '{uri}' must use HTTPS or explicitly admitted HTTP.");
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !AllowHttp)
        {
            throw new PolicyViolationException($"Plain HTTP source '{uri}' is disabled. Pass --allow-http only for a source you control.");
        }

        if (!_allowedHosts.Contains(uri.IdnHost))
        {
            throw new PolicyViolationException(
                $"Host '{uri.IdnHost}' is not admitted. Pass --allow-host {uri.IdnHost} only after establishing trust.");
        }
    }

    private static string NormalizeHost(string value)
    {
        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.IdnHost;
        }

        var separator = trimmed.IndexOf(':');
        return separator > 0 ? trimmed[..separator] : trimmed;
    }
}
