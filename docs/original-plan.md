# plan.md

# dotnet-sdk
## A Cross-Platform .NET SDK Toolchain Manager

Version: 1.0
Status: Product Definition
Repository: github.com/<org>/dotnet-sdk
Package: dotnet-sdk
Command: dotnet-sdk

---

# 1. Mission

Provide a first-class SDK toolchain management experience for .NET.

The tool must allow developers, CI systems, containers, and enterprise administrators to discover, install, update, pin, audit, and prune .NET SDKs from a single CLI experience.

Design goal:

```bash
dotnet-sdk install lts
dotnet-sdk update
dotnet-sdk list
dotnet-sdk pin
```

should feel as natural as:

```bash
dotnet workload install
dotnet tool install
```

The project should be modeled after:

- rustup
- sdkman
- pyenv
- nvm

while remaining idiomatic to the .NET ecosystem.

---

# 2. Product Boundaries

## In Scope

- SDK discovery
- SDK installation
- SDK update
- global.json management
- SDK cleanup
- CI resolution
- Release metadata consumption
- Architecture awareness
- Runtime awareness
- Workload coordination
- Machine-readable output

## Out of Scope

- Custom .NET runtime patching
- Non-Microsoft SDK distribution
- Custom package feeds for SDK binaries
- Visual Studio installation management
- IDE configuration
- Runtime debugging tools

---

# 3. Product Principles

## Principle 1

Do not be an installer.

Be a toolchain manager.

---

## Principle 2

Never scrape websites.

Use official machine-readable metadata.

---

## Principle 3

Never modify user machines without explicit consent.

---

## Principle 4

All operations must be idempotent.

Running the same command repeatedly produces identical end state.

---

## Principle 5

JSON-first architecture.

Everything exposed to humans must also be available to machines.

---

## Principle 6

Offline-friendly operation through cache.

---

## Principle 7

All state transitions must be observable.

Everything can be dry-run.

---

# 4. User Personas

## Developer

Install SDKs.

```bash
dotnet-sdk install lts
```

---

## OSS Maintainer

Test multiple SDK versions.

```bash
dotnet-sdk install 8
dotnet-sdk install 9
dotnet-sdk install 10
```

---

## Enterprise Administrator

Audit installed SDKs.

```bash
dotnet-sdk inventory
```

---

## CI/CD Pipeline

Resolve latest available channel.

```bash
dotnet-sdk resolve lts
```

---

## Container Author

Install reproducible SDK versions.

```bash
dotnet-sdk install 10.0.303
```

---

# 5. Primary CLI Surface

---

## Discovery

### List

```bash
dotnet-sdk list
```

Shows:

- installed SDKs
- active SDK
- available channels
- latest versions

---

### Installed

```bash
dotnet-sdk list installed
```

---

### Remote

```bash
dotnet-sdk list remote
```

---

### Channels

```bash
dotnet-sdk list channels
```

---

### Doctor

```bash
dotnet-sdk doctor
```

Validation:

- PATH
- SDK locations
- architecture
- corrupted installs
- workloads
- global.json

---

### Check

```bash
dotnet-sdk check
```

Shows update availability.

---

# 6. Installation Commands

---

## Install Latest LTS

```bash
dotnet-sdk install lts
```

---

## Install STS

```bash
dotnet-sdk install sts
```

---

## Install Latest

```bash
dotnet-sdk install latest
```

---

## Install Preview

```bash
dotnet-sdk install preview
```

---

## Install Major

```bash
dotnet-sdk install 10
```

Resolves latest:

```text
10.x.x
```

---

## Install Feature Band

```bash
dotnet-sdk install 10.0
```

Resolves latest:

```text
10.0.xxx
```

---

## Exact Version

```bash
dotnet-sdk install 10.0.303
```

---

## Architecture Override

```bash
dotnet-sdk install lts --arch x64
```

```bash
dotnet-sdk install lts --arch arm64
```

---

## Dry Run

```bash
dotnet-sdk install lts --dry-run
```

---

# 7. Update Commands

---

## Update Current Channel

```bash
dotnet-sdk update
```

---

## Update LTS

```bash
dotnet-sdk update --channel lts
```

---

## Update Major

```bash
dotnet-sdk update 10
```

---

## Include Preview

```bash
dotnet-sdk update --preview
```

---

# 8. Global.json Commands

---

## Show

```bash
dotnet-sdk pin show
```

---

## Pin Current

```bash
dotnet-sdk pin
```

Produces:

```json
{
  "sdk": {
    "version": "10.0.303"
  }
}
```

---

## Pin Specific

```bash
dotnet-sdk pin 10.0.303
```

---

## Unpin

```bash
dotnet-sdk pin remove
```

---

## Upgrade

```bash
dotnet-sdk pin update
```

Updates existing global.json.

---

# 9. Cleanup Commands

---

## Prune

```bash
dotnet-sdk prune
```

Keep latest SDK from every channel.

---

## Keep Count

```bash
dotnet-sdk prune --keep 3
```

---

## Preview

```bash
dotnet-sdk prune --dry-run
```

---

## Remove Specific

```bash
dotnet-sdk remove 8.0.412
```

---

# 10. CI Commands

---

## Resolve

```bash
dotnet-sdk resolve lts
```

Output:

```text
10.0.303
```

---

## Resolve JSON

```bash
dotnet-sdk resolve lts --json
```

Output:

```json
{
  "channel":"lts",
  "sdkVersion":"10.0.303"
}
```

---

## Path

```bash
dotnet-sdk path 10.0.303
```

Returns installation directory.

---

# 11. Inventory Commands

---

## Inventory

```bash
dotnet-sdk inventory
```

---

## JSON

```bash
dotnet-sdk inventory --json
```

---

## Enterprise Report

```bash
dotnet-sdk inventory --output report.json
```

---

# 12. Workload Commands

---

## Audit

```bash
dotnet-sdk workloads audit
```

---

## Update

```bash
dotnet-sdk workloads update
```

Internally:

```bash
dotnet workload update
```

---

## Repair

```bash
dotnet-sdk workloads repair
```

---

# 13. Future Commands

---

## Toolchains

```bash
dotnet-sdk toolchain install stable
```

---

## Use

```bash
dotnet-sdk use 10
```

Creates:

```json
global.json
```

---

## Security Audit

```bash
dotnet-sdk advisories
```

---

## SBOM

```bash
dotnet-sdk sbom
```

---

# 14. Machine Output Formats

Every command supports:

```bash
--json
```

```bash
--yaml
```

```bash
--quiet
```

---

Example

```bash
dotnet-sdk list --json
```

```json
{
  "installed":[
    {
      "version":"10.0.303"
    }
  ]
}
```

---

# 15. Official Artifact Sources

The tool must never scrape HTML.

---

## Source Definition

### Releases Index

Official source of channels.

Artifact:

```text
releases-index.json
```

Purpose:

- channel discovery
- channel metadata
- releases endpoint discovery

---

### Channel Releases

Artifact:

```text
releases.json
```

Purpose:

- SDK releases
- runtime releases
- download URLs
- support metadata

---

### Dotnet Installs

Artifacts:

```text
dotnet-install.ps1
dotnet-install.sh
```

Purpose:

- cross-platform installation orchestration

---

### Local Machine

Command:

```bash
dotnet --list-sdks
```

Purpose:

Installed inventory.

---

### Runtime Inventory

Command:

```bash
dotnet --list-runtimes
```

Purpose:

Installed runtimes.

---

# 16. Domain Model

---

## SdkChannel

```csharp
public sealed class SdkChannel
{
    public string Name { get; init; }
    public string ChannelVersion { get; init; }

    public bool IsLts { get; init; }
    public bool IsSts { get; init; }
    public bool IsPreview { get; init; }

    public string LatestSdk { get; init; }
}
```

---

## SdkRelease

```csharp
public sealed class SdkRelease
{
    public string Version { get; init; }

    public DateTime ReleaseDate { get; init; }

    public bool IsPreview { get; init; }

    public bool IsSecurityRelease { get; init; }

    public Uri DownloadUrl { get; init; }

    public string Sha512 { get; init; }
}
```

---

## InstalledSdk

```csharp
public sealed class InstalledSdk
{
    public string Version { get; init; }

    public string Path { get; init; }

    public Architecture Architecture { get; init; }
}
```

---

## GlobalJson

```csharp
public sealed class GlobalJson
{
    public string Version { get; init; }

    public string RollForward { get; init; }

    public bool AllowPrerelease { get; init; }
}
```

---

## InstallationRequest

```csharp
public sealed class InstallationRequest
{
    public string RequestedVersion { get; init; }

    public Architecture Architecture { get; init; }

    public bool PreviewAllowed { get; init; }

    public bool DryRun { get; init; }
}
```

---

# 17. Resolution Engine

Supported inputs:

```text
lts
sts
latest
preview
8
9
10
10.0
10.0.303
```

Examples:

```text
lts      -> latest LTS SDK
10       -> latest 10.x SDK
10.0     -> latest 10.0.xxx SDK
10.0.303 -> exact match
```

Algorithm:

1. Parse token.
2. Determine token type.
3. Resolve against metadata.
4. Verify support status.
5. Return canonical version.

---

# 18. Installation Engine

Responsibilities:

- metadata resolution
- download acquisition
- checksum validation
- installer execution
- inventory refresh

Pipeline:

```text
Resolve
Validate
Acquire
Verify
Install
Validate
Refresh Cache
Report
```

---

# 19. Cache Design

Windows

```text
%LOCALAPPDATA%\dotnet-sdk
```

Linux

```text
~/.cache/dotnet-sdk
```

macOS

```text
~/Library/Caches/dotnet-sdk
```

Structure:

```text
cache/

    metadata/
    downloads/
    installations/
    logs/
```

---

# 20. Configuration

Location:

Windows

```text
%APPDATA%\dotnet-sdk\config.json
```

Linux

```text
~/.config/dotnet-sdk/config.json
```

macOS

```text
~/Library/Application Support/dotnet-sdk/config.json
```

Schema:

```json
{
  "defaultChannel":"lts",
  "allowPreview":false,
  "pruneKeep":3,
  "telemetry":false
}
```

---

# 21. Repository Layout

```text
src/

  DotNetSdk.Cli/

  DotNetSdk.Application/

  DotNetSdk.Core/

  DotNetSdk.Infrastructure/

  DotNetSdk.Installer/

  DotNetSdk.Metadata/

tests/

  DotNetSdk.UnitTests/

  DotNetSdk.IntegrationTests/

  DotNetSdk.EndToEndTests/

docs/

  architecture.md
  cli.md
  metadata.md
  security.md

eng/

  pipelines/
  packaging/

samples/

tools/
```

---

# 22. Application Layers

Layer 1

```text
CLI
```

System.CommandLine

---

Layer 2

```text
Application
```

Use cases.

---

Layer 3

```text
Domain
```

Business rules.

---

Layer 4

```text
Infrastructure
```

Filesystem
HTTP
Process execution

---

Layer 5

```text
Platform
```

Windows
Linux
macOS

---

# 23. Abstractions

```csharp
public interface IReleaseMetadataProvider
{
    Task<ChannelCatalog> GetChannels();
}
```

```csharp
public interface ISdkInstaller
{
    Task Install(InstallationRequest request);
}
```

```csharp
public interface ISdkLocator
{
    Task<IReadOnlyList<InstalledSdk>> GetInstalled();
}
```

```csharp
public interface IGlobalJsonManager
{
    Task Pin(string version);
}
```

```csharp
public interface IChecksumValidator
{
    Task<bool> Verify();
}
```

---

# 24. Exit Codes

```text
0 Success

1 UnexpectedFailure

2 InvalidArguments

3 VersionNotFound

4 NetworkFailure

5 InstallationFailure

6 AlreadyInstalled

7 ValidationFailure

8 PermissionDenied
```

---

# 25. Observability

Logging Levels:

```text
Trace
Debug
Information
Warning
Error
Critical
```

Flags:

```bash
--verbose
```

```bash
--trace
```

---

# 26. Telemetry Policy

Default:

```text
Disabled
```

Enable:

```bash
dotnet-sdk config telemetry enable
```

Requirements:

- anonymous
- opt-in
- documented
- removable

---

# 27. Security Requirements

Every download:

```text
Must verify checksum.
```

---

Every installer:

```text
Must execute from trusted source.
```

---

No silent elevation.

---

No shell injection opportunities.

---

No arbitrary URL execution.

---

# 28. Testing Strategy

## Unit

Coverage target:

```text
90%
```

---

## Integration

Use:

```text
Real release metadata
Fake installers
```

---

## E2E

Matrix:

```text
Windows x64
Windows arm64

Linux x64
Linux arm64

macOS x64
macOS arm64
```

---

# 29. Gates

## PR Gate

Must pass:

```text
Build
Unit Tests
Formatting
Analyzers
Security Scan
```

---

## Merge Gate

Must pass:

```text
Integration Tests
E2E Tests
Package Validation
```

---

## Release Gate

Must pass:

```text
Version Audit
Artifact Verification
SBOM Generation
Signing Verification
```

---

# 30. Quality Standards

Nullable enabled:

```xml
<Nullable>enable</Nullable>
```

---

Warnings as errors:

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

---

Analyzers:

```text
Microsoft.CodeAnalysis.NetAnalyzers
```

---

Formatting:

```bash
dotnet format
```

---

# 31. Packaging

Package:

```text
dotnet-sdk
```

Tool Command:

```text
dotnet-sdk
```

Install:

```bash
dotnet tool install -g dotnet-sdk
```

Update:

```bash
dotnet tool update -g dotnet-sdk
```

---

# 32. Milestones

## M1

Metadata
Inventory
Listing

---

## M2

Resolution Engine
Install Engine

---

## M3

Update
Pin
Prune

---

## M4

JSON APIs
CI Support

---

## M5

Workload Integration
Enterprise Inventory

---

## M6

Security
SBOM
Advisories

---

# 33. Definition of Done

A feature is complete only when:

- Implemented
- Unit tested
- Integration tested
- CLI documented
- JSON output documented
- Exit codes documented
- Security reviewed
- Analyzer clean
- Cross-platform validated
- Included in release notes

---

# 34. North Star Outcome

A developer with a fresh machine can execute:

```bash
dotnet tool install -g dotnet-sdk

dotnet-sdk install lts

dotnet-sdk pin

dotnet build
```

without visiting a website, downloading an installer manually, searching for release versions, or understanding SDK distribution details.

The tool becomes the canonical SDK toolchain manager for the .NET ecosystem.