#!/usr/bin/env bash
# Runs the unit and integration test suites (no Docker required).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

dotnet test tests/DotnetSdkManager.UnitTests/DotnetSdkManager.UnitTests.csproj
dotnet test tests/DotnetSdkManager.IntegrationTests/DotnetSdkManager.IntegrationTests.csproj
