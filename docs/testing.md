# Testing

## Unit suite

Covers deterministic logic and security boundaries:

- SDK version parsing and ordering.
- release metadata variants.
- LTS/STS/EOL and preview resolution.
- source allowlisting and HTTP policy.
- SHA-512 formats and mismatch handling.
- historical RID ranking.
- ZIP/TAR traversal and link rejection.
- legacy-aware `global.json` output.

## Integration suite

Runs against an in-process loopback HTTP server and real temporary files:

- metadata ETag caching and offline reuse.
- archive download and checksum verification.
- atomic install and idempotent repeat.
- checksum and extraction failures leave no final root.
- activation, shim creation, inventory, and project pinning.

## E2E suite

The Testcontainers test starts:

1. An NGINX container serving a synthetic .NET release feed and SDK archive.
2. A separate .NET SDK container running the compiled CLI against that feed.

It verifies resolution, download, validation, installation, activation, shim execution, list output, and `global.json` behavior across the Docker network.

E2E execution is opt-in so ordinary builds remain usable on machines without Docker:

```bash
RUN_E2E=1 dotnet test tests/DotnetSdkManager.E2ETests
```

When enabled, Docker must be reachable by Testcontainers and the configured container images must be available locally or pullable.
