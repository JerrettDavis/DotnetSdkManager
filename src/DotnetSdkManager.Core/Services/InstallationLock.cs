namespace DotnetSdkManager.Services;

internal sealed class InstallationLock : IAsyncDisposable
{
    private readonly FileStream _stream;

    private InstallationLock(FileStream stream)
    {
        _stream = stream;
    }

    public static async Task<InstallationLock> AcquireAsync(
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new InstallationLock(stream);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(125), cancellationToken);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        return ValueTask.CompletedTask;
    }
}
