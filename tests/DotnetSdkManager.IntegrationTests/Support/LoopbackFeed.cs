using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DotnetSdkManager.Security;

namespace DotnetSdkManager.IntegrationTests.Support;

internal sealed class LoopbackFeed : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<string, Route> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task _serverLoop;
    private int _notModifiedCount;

    private LoopbackFeed(Uri baseUri)
    {
        BaseUri = baseUri;
        _listener.Prefixes.Add(baseUri.AbsoluteUri);
        _listener.Start();
        _serverLoop = RunAsync();
    }

    public Uri BaseUri { get; }

    public Uri ReleasesIndex => new(BaseUri, "release-metadata/releases-index.json");

    public int NotModifiedCount => Volatile.Read(ref _notModifiedCount);

    public static async Task<LoopbackFeed> CreateAsync(
        string workDirectory,
        string version = "10.0.100",
        string rid = "linux-x64",
        bool badChecksum = false,
        bool traversalArchive = false)
    {
        var port = ReservePort();
        var feed = new LoopbackFeed(new Uri($"http://127.0.0.1:{port}/"));
        var archivePath = Path.Combine(workDirectory, $"dotnet-sdk-{version}-{rid}.zip");
        await CreateArchiveAsync(archivePath, version, traversalArchive);
        var hash = await HashVerifier.ComputeSha512Async(archivePath);
        if (badChecksum)
        {
            hash = new string('0', 128);
        }

        var releasesUri = new Uri(feed.BaseUri, "release-metadata/10.0/releases.json");
        var archiveUri = new Uri(feed.BaseUri, $"archives/{Path.GetFileName(archivePath)}");
        var index = $$"""
            {
              "releases-index": [
                {
                  "channel-version": "10.0",
                  "latest-sdk": "{{version}}",
                  "support-phase": "active",
                  "release-type": "lts",
                  "eol-date": "2099-01-01",
                  "releases.json": "{{releasesUri.AbsoluteUri}}"
                }
              ]
            }
            """;
        var releases = $$"""
            {
              "releases": [
                {
                  "release-date": "2026-01-01",
                  "sdks": [
                    {
                      "version": "{{version}}",
                      "files": [
                        {
                          "name": "{{Path.GetFileName(archivePath)}}",
                          "rid": "{{rid}}",
                          "url": "{{archiveUri.AbsoluteUri}}",
                          "hash": "{{hash}}"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        feed.Add("/release-metadata/releases-index.json", Encoding.UTF8.GetBytes(index), "application/json", "\"index-v1\"");
        feed.Add("/release-metadata/10.0/releases.json", Encoding.UTF8.GetBytes(releases), "application/json", "\"releases-v1\"");
        feed.Add($"/archives/{Path.GetFileName(archivePath)}", await File.ReadAllBytesAsync(archivePath), "application/zip", "\"archive-v1\"");
        return feed;
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        _listener.Close();
        try
        {
            await _serverLoop;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpListenerException)
        {
        }

        _stop.Dispose();
    }

    private void Add(string path, byte[] body, string contentType, string etag) =>
        _routes[path] = new Route(body, contentType, etag);

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (_stop.IsCancellationRequested)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context), _stop.Token);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (!_routes.TryGetValue(path, out var route))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                return;
            }

            if (string.Equals(context.Request.Headers["If-None-Match"], route.ETag, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _notModifiedCount);
                context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                context.Response.Close();
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = route.ContentType;
            context.Response.Headers["ETag"] = route.ETag;
            context.Response.ContentLength64 = route.Body.Length;
            await context.Response.OutputStream.WriteAsync(route.Body, _stop.Token);
            context.Response.Close();
        }
        catch (Exception) when (_stop.IsCancellationRequested)
        {
        }
    }

    private static async Task CreateArchiveAsync(string archivePath, string version, bool traversal)
    {
        await using var file = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true);
        var entryName = traversal ? "../escaped.txt" : OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        await writer.WriteAsync(OperatingSystem.IsWindows()
            ? version
            : $"#!/bin/sh\nif [ \"${{1:-}}\" = \"--version\" ]; then echo \"{version}\"; else echo \"fake sdk {version}\"; fi\n");
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed record Route(byte[] Body, string ContentType, string ETag);
}
