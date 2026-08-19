# DotnetSdkManager

`dotnet-sdk-manager` is a user-scoped .NET SDK installer, selector, and project pinning tool. It downloads SDK archives itself through `HttpClient`, verifies every archive with SHA-512, extracts into isolated version roots, and never modifies SDKs owned by an operating-system package manager.

The source uses .NET 10 and C# 14. The canonical distribution is a RID-specific, self-contained executable, so the machine does not need a sufficiently new .NET runtime merely to run the manager. A .NET global-tool package is included as a convenience for machines that already have .NET 10.

## What it does

- Resolves stable, preview, LTS, STS, current, and EOL channels from official .NET release metadata.
- Installs each SDK under an isolated, user-owned root and switches versions through a small PATH shim.
- Pins projects with a generation-aware `global.json` writer.
- Supports old metadata shapes, historical RID aliases, explicit artifact RIDs, local archives, custom feeds, proxies, and offline metadata cache use.
- Treats system/package-manager SDKs as read-only inventory.
- Rejects unapproved hosts, insecure HTTP by default, redirects to unapproved hosts, missing checksums, checksum mismatches, traversal paths, and archive links.

## Important compatibility boundary

Installing an old SDK archive is not the same as making that SDK runnable on a modern operating system. Old SDKs can depend on native OpenSSL, ICU, libc, or operating-system versions that no longer exist. The manager can securely acquire and isolate those SDKs, report the selected historical artifact RID, and run diagnostics, but it does not emulate an old operating system or fabricate native dependencies.

Likewise, no .NET global tool can bootstrap a machine whose installed runtime is too old to execute that global tool. Use a self-contained release binary for that scenario.

## Quick start from source

```bash
dotnet build DotnetSdkManager.slnx
dotnet test DotnetSdkManager.slnx --no-build
```

Publish a standalone binary:

```bash
./scripts/publish.sh linux-x64
# Windows PowerShell:
./scripts/publish.ps1 win-x64
```

Install and activate the newest supported LTS SDK:

```bash
dotnet-sdk-manager install --channel LTS --activate
```

Install an exact EOL SDK, explicitly accepting the risk:

```bash
dotnet-sdk-manager install 2.1.818 --allow-eol --activate
```

Pin a project while omitting fields old SDK generations do not understand:

```bash
dotnet-sdk-manager use 2.1.818 --path ./legacy-project
```

Use an offline archive with a mandatory checksum:

```bash
dotnet-sdk-manager install 6.0.428 \
  --archive ./dotnet-sdk-6.0.428-linux-x64.tar.gz \
  --sha512 <sha512> --artifact-rid linux-x64
```

Run `dotnet-sdk-manager help` for all commands and options.

## Local test suites

```bash
# Unit and integration tests
./scripts/test.sh

# Docker-backed, two-container E2E test
RUN_E2E=1 ./scripts/test-e2e.sh
```

The E2E suite uses Testcontainers to start an isolated HTTP release feed and a separate .NET CLI container. It exercises metadata resolution, download, checksum verification, secure extraction, activation, shim execution, inventory, and legacy-aware `global.json` creation.

## Repository guide

- [`docs/plan-review.md`](docs/plan-review.md): issues found in the original proposal and the revised plan.
- [`docs/architecture.md`](docs/architecture.md): components and install transaction.
- [`docs/compatibility.md`](docs/compatibility.md): current and legacy behavior.
- [`docs/security.md`](docs/security.md): threat model and trust boundaries.
- [`docs/testing.md`](docs/testing.md): test pyramid and local commands.
- [`docs/original-plan.md`](docs/original-plan.md): the supplied plan, preserved verbatim.

## Status

This repository is an implementation-ready reference release. Before publishing under a public package ID, choose a final product name, reserve package/repository names, establish signing and release provenance, and wire the release workflow to your organization’s signing infrastructure.
