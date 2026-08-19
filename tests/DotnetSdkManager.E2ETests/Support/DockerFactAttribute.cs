using Xunit;

namespace DotnetSdkManager.E2ETests.Support;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_E2E"), "1", StringComparison.Ordinal))
        {
            Skip = "Set RUN_E2E=1 to run the Docker-backed Testcontainers suite.";
        }
    }
}
