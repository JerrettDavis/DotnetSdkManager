#!/usr/bin/env pwsh
# Publishes a self-contained, single-file dotnet-sdk-manager binary for one RID.
# Usage: ./scripts/publish.ps1 <rid>   e.g. ./scripts/publish.ps1 win-x64
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Rid
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$out = ".artifacts/publish/$Rid"

dotnet publish src/DotnetSdkManager.Cli/DotnetSdkManager.Cli.csproj `
  --configuration Release `
  --runtime $Rid `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=false `
  --output $out

Write-Host "Published to $out"
