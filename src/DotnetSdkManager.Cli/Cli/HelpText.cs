namespace DotnetSdkManager.Cli;

internal static class HelpText
{
    public const string Text = """
        DotnetSdkManager 0.1.0

        Secure, user-scoped .NET SDK installation and selection.

        Usage:
          dotnet-sdk-manager <command> [arguments] [options]

        Commands:
          available              List SDKs available for a channel and RID.
          install [version]      Install an exact SDK or the newest SDK in a channel.
          upgrade                Install and activate the newest SDK in a channel.
          list                   List managed and read-only system SDK inventory.
          activate <version>     Select an installed managed SDK for the PATH shim.
          use <version>          Write a generation-compatible global.json.
          remove <version>       Remove only a user-owned managed SDK.
          env                    Print a shell command that prepends the shim directory.
          doctor                 Check manager state and execute the active SDK.
          cache clean            Clear metadata and download caches.
          help                   Show this help.

        Common source options:
          --index <uri>          Release-index URI. Defaults to the official .NET index.
          --allow-host <host>    Admit an additional exact host; may be repeated.
          --allow-http           Permit HTTP as well as HTTPS. Intended for controlled feeds.
          --offline              Use cached metadata and do not make metadata requests.
          --home <path>          Override the manager home for this invocation.

        Resolution options:
          --channel <value>      LTS, STS, CURRENT, or an exact major.minor channel.
          --rid <rid>            Target installation RID. Defaults to the current process RID.
          --artifact-rid <rid>   Require an exact release artifact RID.
          --include-preview      Admit prerelease SDKs and preview/go-live channels.
          --allow-eol            Admit end-of-life channels.

        Install options:
          --archive <path>       Install a local SDK archive; exact version and SHA-512 required.
          --url <uri>            Install a direct SDK archive; exact version and SHA-512 required.
          --sha512 <value>       Trusted SHA-512 in hexadecimal or Base64.
          --activate             Activate after installation.
          --force                Atomically replace a different managed artifact.

        Other options:
          --json                 Emit JSON where supported.
          --managed-only         Omit read-only system inventory from list.
          --roll-forward <mode>  Set global.json rollForward for SDK 3.0 or newer.
          --allow-prerelease     Set global.json allowPrerelease to true.
          --no-allow-prerelease  Set global.json allowPrerelease to false.
          --allow-uninstalled    Permit pinning an SDK absent from managed inventory.
          --shell <name>         bash, zsh, sh, fish, powershell, or cmd.
          --no-activate          Do not activate after upgrade.
          -h, --help             Show help.
          --version              Show the manager version.

        Security defaults:
          * Package-manager SDKs are never changed.
          * HTTPS and an admitted host are required.
          * Every archive requires SHA-512 verification.
          * EOL and preview selection are explicit opt-ins.
          * Archive traversal and link entries are rejected.
        """;
}
