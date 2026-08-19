using DotnetSdkManager.Configuration;

namespace DotnetSdkManager.Services;

public sealed class EnvironmentService
{
    private readonly ManagerPaths _paths;

    public EnvironmentService(ManagerPaths paths)
    {
        _paths = paths;
    }

    public string GetInstructions(string? shell)
    {
        shell = string.IsNullOrWhiteSpace(shell) ? DetectShell() : shell.Trim().ToLowerInvariant();
        return shell switch
        {
            "powershell" or "pwsh" => $"$env:PATH = '{EscapePowerShell(_paths.Bin)};' + $env:PATH",
            "cmd" => $"set \"PATH={_paths.Bin};%PATH%\"",
            "fish" => $"fish_add_path --prepend '{EscapeSingleQuotes(_paths.Bin)}'",
            "bash" or "zsh" or "sh" => $"export PATH='{EscapeSingleQuotes(_paths.Bin)}':\"$PATH\"",
            _ => throw new ArgumentException($"Unsupported shell '{shell}'. Use bash, zsh, sh, fish, powershell, or cmd.")
        };
    }

    private static string DetectShell()
    {
        if (OperatingSystem.IsWindows())
        {
            return "powershell";
        }

        var shell = Path.GetFileName(Environment.GetEnvironmentVariable("SHELL"));
        return string.IsNullOrWhiteSpace(shell) ? "sh" : shell;
    }

    private static string EscapeSingleQuotes(string value) => value.Replace("'", "'\\''", StringComparison.Ordinal);

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
