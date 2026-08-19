# Verification

Run the full local pipeline:

```bash
./eng/verify.sh          # restore, build, unit + integration tests
RUN_E2E=1 ./eng/verify.sh # also runs the Docker-backed Testcontainers E2E suite
```

As of the last verification pass: build is clean (0 warnings, 0 errors), 38 unit tests pass, 4 integration tests pass, and the Testcontainers E2E suite (synthetic release feed + `.NET` CLI container) passes, exercising install, activation, shim execution, `global.json` pinning, and inventory listing end to end.

CI (`.github/workflows/ci.yml`) runs the same restore/build/unit/integration/E2E sequence on every push and pull request.
