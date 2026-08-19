# Plan review and revised implementation plan

## Executive assessment

The original direction is sound: a small tool should be able to discover, acquire, verify, install, select, and remove .NET SDKs without delegating to `winget`, `apt`, Homebrew, `curl`, or another external downloader. The plan needed stronger boundaries around bootstrapping, ownership, historical compatibility, release metadata, integrity, and failure recovery.

This repository revises the plan around one rule: **manage only user-owned, archive-based SDK installations and regard every other installation source as read-only inventory**.

## Issues addressed

### 1. A global tool is not a universal bootstrap

A framework-dependent global tool needs a compatible .NET runtime before it can start. That creates the exact deadlock the product is intended to solve on old or incomplete machines.

**Revision:** produce RID-specific, self-contained, single-file binaries as the canonical release. Keep `PackAsTool` only as a convenience distribution for machines already capable of running the package target framework.

### 2. “Supports old SDKs” needed a precise definition

The manager can parse old release metadata, select historical artifacts, extract them, isolate them, activate them, and write an old-compatible `global.json`. It cannot guarantee that a .NET Core 1.x SDK will execute on a modern Linux distribution or unsupported Windows/macOS release. Historical SDKs may require native dependencies or kernels that are no longer present.

**Revision:** separate four capabilities:

1. Manager runtime compatibility.
2. Artifact availability for the target architecture/RID.
3. Secure installation compatibility.
4. Actual SDK runtime compatibility with the host OS and native libraries.

The `doctor` command reports the fourth category instead of pretending installation proves executability.

### 3. Shared-root overlay installation is unsafe

Extracting many archives into a common `DOTNET_ROOT` makes ownership ambiguous, permits stale files to survive upgrades, complicates rollback, and makes removal unsafe.

**Revision:** install each SDK into `sdks/<version>/<target-rid>/`. Activation changes only a small current-root pointer and a user-owned shim. A failed install never mutates the active SDK.

### 4. Package-manager SDKs must not be mutated

Deleting or overlaying `/usr/share/dotnet`, Program Files, or Homebrew-managed roots can corrupt the machine’s package database and may require elevation.

**Revision:** parse `dotnet --list-sdks` only for inventory. Install, activate, and remove operations are restricted to the manager home.

### 5. Integrity checks alone were not enough

A checksum fetched from the same compromised location as an archive does not by itself establish authenticity. Automatic redirects can also escape a host allowlist.

**Revision:** combine HTTPS, exact host allowlisting, validation of every redirect target, mandatory SHA-512, atomic cache writes, and secure archive extraction. Custom hosts and HTTP require explicit command-line admission. A missing checksum is a hard failure unless the user supplies one explicitly.

### 6. Archive extraction needed an explicit threat model

Naive ZIP or TAR extraction permits `../` traversal, rooted paths, drive paths, symbolic links, and hard links that escape the staging directory.

**Revision:** manually extract supported archive types, canonicalize every target path, reject links and special files, and stage on the same filesystem before an atomic directory move.

### 7. Release metadata changes across generations

Older channels use both singular `sdk` and plural `sdks` shapes and can publish historical, distro-specific RIDs rather than today’s portable RIDs.

**Revision:** use tolerant JSON parsing, preserve unknown fields, support both shapes, rank historical RID aliases, and expose `--artifact-rid` for deterministic override. Linux fallback never crosses from musl to glibc implicitly.

### 8. `global.json` is version-sensitive

Fields accepted by modern SDKs can cause old SDKs to reject a project configuration.

**Revision:** always write `sdk.version`; write `rollForward` and `allowPrerelease` only for SDK 3.0 or newer; remove unsupported fields when pinning an older SDK; preserve unrelated root and SDK properties.

### 9. EOL acquisition must be an informed opt-in

Silently selecting an unsupported SDK encourages insecure installations.

**Revision:** active supported channels are the default. EOL channels require `--allow-eol`. Preview versions require `--include-preview`. Output and manifests retain support-state information.

### 10. Rollback and concurrency were underspecified

Parallel invocations and interrupted extraction can leave partially installed roots.

**Revision:** use per-version file locks, download to `.part`, verify before extraction, extract to a unique staging directory, write a manifest, atomically move into place, and retain the old root until a forced replacement succeeds.

## Revised delivery plan

### Phase 1: secure manager core

- Release-index and per-channel metadata clients with conditional caching and offline mode.
- Exact-channel, LTS/STS, preview, EOL, RID, and historical-RID resolution.
- Validated HTTP transport, SHA-512 verification, secure ZIP/TAR extraction.
- Isolated and atomic install transaction with manifests and locks.
- Managed/system inventory, activation, removal, environment output, and diagnostics.

### Phase 2: CLI and compatibility surface

- Commands: `available`, `install`, `upgrade`, `list`, `activate`, `use`, `remove`, `env`, `doctor`, and `cache clean`.
- Custom feed, local archive, explicit URL, proxy-compatible HTTP, offline cache, and JSON output.
- Legacy `global.json` capability filtering and historical RID reporting.
- Human-readable errors with stable exit-code categories.

### Phase 3: verification

- Unit tests for version ordering, metadata variants, source policy, hashes, safe extraction, RID selection, and `global.json` compatibility.
- Integration tests for metadata caching, download, checksum failures, idempotent installation, atomic failure behavior, activation, and inventory.
- Testcontainers E2E with a feed container and a separate CLI container.

### Phase 4: release hardening

- Build self-contained artifacts for supported RIDs.
- Generate checksums and an SBOM, sign release artifacts, and publish provenance.
- Reserve package IDs and command names before public publication.
- Test binaries on the oldest operating-system versions supported by the chosen .NET 10 runtime build.
- Add a release-feed mirror policy only if official archival retention becomes insufficient.

## Deliberate non-goals

- Installing native prerequisites for obsolete SDKs.
- Editing or removing package-manager-owned SDKs.
- Replacing project-local workload management.
- Promising unsupported operating-system compatibility.
- Bypassing checksums or silently trusting arbitrary mirrors.
- Automatically changing the user’s shell profile or machine-wide PATH.
