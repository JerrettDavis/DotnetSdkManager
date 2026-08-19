# Architecture

## Components

```text
CLI
 ├─ argument parser and output formatting
 ├─ release catalog
 │   ├─ validated HTTP transport
 │   ├─ conditional metadata cache
 │   └─ tolerant metadata parser
 ├─ installer
 │   ├─ per-SDK lock
 │   ├─ download cache
 │   ├─ SHA-512 verifier
 │   ├─ secure archive extractor
 │   └─ atomic directory commit
 ├─ managed and system inventory
 ├─ activation/shim service
 ├─ global.json compatibility writer
 └─ doctor and environment services
```

## Managed layout

```text
<manager-home>/
  active.json
  current-root
  bin/
    dotnet              # Unix shim
    dotnet.cmd          # Windows shim
  cache/
    metadata/
    downloads/
  locks/
  staging/
  sdks/
    10.0.100/
      linux-x64/
        dotnet
        sdk/
        .dotnet-sdk-manager.json
```

`DOTNET_SDK_MANAGER_HOME` overrides the default. `--home` overrides both for a single invocation.

## Install transaction

```text
Resolve metadata and artifact
        │
        ▼
Validate source URI and every redirect
        │
        ▼
Acquire per-version/RID lock
        │
        ▼
Download to cache .part file
        │
        ▼
Verify mandatory SHA-512
        │
        ▼
Extract into unique staging root
        │
        ▼
Reject traversal, rooted paths, and links
        │
        ▼
Validate dotnet host + write manifest
        │
        ▼
Atomic move into final isolated root
        │
        ▼
Optional activation changes pointer only
```

A forced replacement first moves the previous root to a backup, commits the staged root, and restores the backup if the commit fails.

## Trust boundaries

Release metadata is treated as untrusted input even when retrieved from an admitted HTTPS host. URLs, checksums, JSON shapes, archive entries, paths, redirects, and subprocess output are validated before use.
