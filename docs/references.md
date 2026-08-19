# Primary references

The implementation is designed around the official .NET distribution and configuration surfaces:

- .NET support policy: <https://dotnet.microsoft.com/platform/support/policy/dotnet-core>
- `dotnet-install` script guidance: <https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script>
- `global.json` overview and roll-forward policy: <https://learn.microsoft.com/dotnet/core/tools/global-json>
- Official release index: <https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json>
- .NET release metadata repository and release notes: <https://github.com/dotnet/core/tree/main/release-notes>
- Official install-script source: <https://github.com/dotnet/install-scripts>

These references are inputs to the design, not runtime dependencies. The manager reads the release-index and per-channel JSON directly and does not shell out to the official install scripts.
