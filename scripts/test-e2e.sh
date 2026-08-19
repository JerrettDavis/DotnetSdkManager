#!/usr/bin/env bash
# Runs the Docker-backed Testcontainers E2E suite. Requires a reachable Docker daemon.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

dotnet build src/DotnetSdkManager.Cli/DotnetSdkManager.Cli.csproj
RUN_E2E=1 dotnet test tests/DotnetSdkManager.E2ETests/DotnetSdkManager.E2ETests.csproj
