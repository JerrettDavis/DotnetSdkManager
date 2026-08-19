# Compatibility model

## Manager runtime

Source projects target .NET 10 and C# 14. Public releases should be RID-specific and self-contained. This avoids a dependency on an installed .NET runtime, but the executable still requires an operating system and CPU supported by its .NET 10 runtime build.

## SDK generations

The catalog parser accepts both historical singular `sdk` and modern plural `sdks` release entries. Version comparison supports stable and prerelease SDK labels without taking a dependency on the SDK being managed.

Historical artifact RIDs are ranked behind an exact match. Examples include `win7-x64`, versioned macOS RIDs, and distro-specific Linux RIDs. The chosen artifact RID is always retained in the manifest and displayed when it differs from the target RID. `--artifact-rid` provides an exact override.

A musl target is never silently mapped to a glibc artifact. Cross-architecture emulation is outside the manager’s scope.

## `global.json` shims

| Selected SDK | Written fields |
|---|---|
| 1.x and 2.x | `sdk.version` only |
| 3.x and newer | `sdk.version`, plus requested `rollForward` and `allowPrerelease` |

Unrelated root properties and unrelated properties under `sdk` are preserved. Unsupported modern fields are removed when pinning a legacy SDK.

## EOL versions

EOL channel acquisition requires `--allow-eol`. A manual local archive or direct URL remains possible when official release metadata no longer contains an artifact, but SHA-512 remains mandatory.

## Runtime diagnostics

`doctor` checks whether the selected `dotnet` host can execute. A failure after successful installation usually means the old SDK is incompatible with the current OS/native dependency set, not that the archive transaction was incomplete.
