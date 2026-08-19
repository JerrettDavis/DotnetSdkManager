#!/usr/bin/env bash
# Full local verification pipeline: restore, build, unit + integration tests.
# Set RUN_E2E=1 to also run the Docker-backed Testcontainers E2E suite.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

dotnet restore DotnetSdkManager.slnx
dotnet build DotnetSdkManager.slnx --no-restore --configuration Release
dotnet test tests/DotnetSdkManager.UnitTests/DotnetSdkManager.UnitTests.csproj --no-build --configuration Release
dotnet test tests/DotnetSdkManager.IntegrationTests/DotnetSdkManager.IntegrationTests.csproj --no-build --configuration Release

if [ "${RUN_E2E:-}" = "1" ]; then
  RUN_E2E=1 dotnet test tests/DotnetSdkManager.E2ETests/DotnetSdkManager.E2ETests.csproj --no-build --configuration Release
fi
