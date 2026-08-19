#!/usr/bin/env bash
# Publishes a self-contained, single-file dotnet-sdk-manager binary for one RID.
# Usage: ./scripts/publish.sh <rid>   e.g. ./scripts/publish.sh linux-x64
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

rid="${1:?Usage: publish.sh <rid> (e.g. linux-x64, win-x64, osx-arm64)}"
out=".artifacts/publish/${rid}"

dotnet publish src/DotnetSdkManager.Cli/DotnetSdkManager.Cli.csproj \
  --configuration Release \
  --runtime "${rid}" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  --output "${out}"

echo "Published to ${out}"
