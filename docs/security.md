# Security model

## Default source policy

The transport admits official .NET release metadata and artifact hosts over HTTPS. Every redirect is resolved manually and subjected to the same policy. Arbitrary custom hosts require one or more `--allow-host` values. Plain HTTP additionally requires `--allow-http`.

## Archive integrity

Every install requires an expected SHA-512 value from release metadata or an explicit `--sha512` argument. Cached downloads are reverified before use. A mismatch deletes the candidate cache entry and aborts without touching an installed SDK.

## Extraction controls

- Canonical target paths must remain inside the staging root.
- Absolute, rooted, drive-qualified, and traversal paths are rejected.
- ZIP symbolic links, TAR symbolic links, TAR hard links, devices, and other special entries are rejected.
- Extraction occurs in a unique staging directory.
- The final install is committed only after validation.

## Ownership controls

All mutation is constrained to the manager home. Package-manager and system SDKs are inventory-only. The tool does not request elevation and does not edit shell profiles automatically.

## Residual risks

- HTTPS plus a hash from release metadata does not replace signed release provenance. Public releases of this manager should themselves be signed and accompanied by provenance and an SBOM.
- An admitted custom host is a user-granted trust decision.
- Running an SDK executes code from that SDK distribution. EOL SDKs may contain known vulnerabilities.
- A privileged local attacker can alter user-owned manager state.
