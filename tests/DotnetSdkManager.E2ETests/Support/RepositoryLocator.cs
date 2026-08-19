namespace DotnetSdkManager.E2ETests.Support;

internal static class RepositoryLocator
{
    public static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DotnetSdkManager.slnx")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the DotnetSdkManager repository root.");
    }

    public static string FindCliOutput(string repositoryRoot)
    {
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = testOutput.Parent?.Name ?? "Debug";
        var expected = Path.Combine(
            repositoryRoot,
            "src",
            "DotnetSdkManager.Cli",
            "bin",
            configuration,
            "net10.0");
        if (!File.Exists(Path.Combine(expected, "dotnet-sdk-manager.dll")))
        {
            throw new FileNotFoundException(
                "The CLI build output was not found. Build the solution before running E2E tests.",
                Path.Combine(expected, "dotnet-sdk-manager.dll"));
        }

        return expected;
    }
}
