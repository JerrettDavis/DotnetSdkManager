namespace DotnetSdkManager.Exceptions;

public class SdkManagerException : Exception
{
    public SdkManagerException(string message, int exitCode = 8, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

public sealed class PolicyViolationException : SdkManagerException
{
    public PolicyViolationException(string message)
        : base(message, 4)
    {
    }
}

public sealed class IntegrityException : SdkManagerException
{
    public IntegrityException(string message)
        : base(message, 6)
    {
    }
}

public sealed class ResolutionException : SdkManagerException
{
    public ResolutionException(string message)
        : base(message, 7)
    {
    }
}

public sealed class InstallationException : SdkManagerException
{
    public InstallationException(string message, Exception? innerException = null)
        : base(message, 8, innerException)
    {
    }
}
