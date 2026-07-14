[CmdletBinding()]
param(
    [string]$FeedPath,
    [string]$CoreContractsProjectPath,
    [string]$PluginSdkProjectPath,
    [string]$PluginIsolationContractsProjectPath,
    [string]$SharedIpcProjectPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($FeedPath)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $FeedPath = Join-Path (Resolve-Path (Join-Path $scriptRoot "..")).Path "packages"
}

if ([string]::IsNullOrWhiteSpace($PluginSdkProjectPath)) {
    $PluginSdkProjectPath = (Resolve-Path "..\LanMountainDesktop\LanMountainDesktop.PluginSdk\LanMountainDesktop.PluginSdk.csproj").Path
}

if ([string]::IsNullOrWhiteSpace($CoreContractsProjectPath)) {
    $CoreContractsProjectPath = (Resolve-Path "..\LanMountainDesktop\LanMountainDesktop.Shared.Contracts\LanMountainDesktop.Shared.Contracts.csproj").Path
}

if ([string]::IsNullOrWhiteSpace($PluginIsolationContractsProjectPath)) {
    $PluginIsolationContractsProjectPath = (Resolve-Path "..\LanMountainDesktop\LanMountainDesktop.PluginIsolation.Contracts\LanMountainDesktop.PluginIsolation.Contracts.csproj").Path
}

if ([string]::IsNullOrWhiteSpace($SharedIpcProjectPath)) {
    $SharedIpcProjectPath = (Resolve-Path "..\LanMountainDesktop\LanMountainDesktop.Shared.IPC\LanMountainDesktop.Shared.IPC.csproj").Path
}

function Pack-Project([string]$ProjectPath, [string]$OutputDirectory) {
    if (-not (Test-Path $ProjectPath)) {
        throw "Project '$ProjectPath' was not found."
    }

    dotnet restore $ProjectPath --force --no-cache | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for '$ProjectPath'."
    }

    dotnet pack $ProjectPath -c Release -o $OutputDirectory -p:ContinuousIntegrationBuild=true --no-restore | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for '$ProjectPath'."
    }
}

New-Item -ItemType Directory -Force -Path $FeedPath | Out-Null
Pack-Project -ProjectPath $CoreContractsProjectPath -OutputDirectory $FeedPath
Pack-Project -ProjectPath $PluginIsolationContractsProjectPath -OutputDirectory $FeedPath
Pack-Project -ProjectPath $SharedIpcProjectPath -OutputDirectory $FeedPath
Pack-Project -ProjectPath $PluginSdkProjectPath -OutputDirectory $FeedPath

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$repositoryPackageCache = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot ".nuget\packages"))
if (-not $repositoryPackageCache.StartsWith(
        $repositoryRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe repository NuGet package cache path '$repositoryPackageCache'."
}
if (Test-Path -LiteralPath $repositoryPackageCache) {
    Remove-Item -LiteralPath $repositoryPackageCache -Recurse -Force
}

Write-Host "Local package feed initialized at '$FeedPath'."
Write-Host "Cleared repository package cache '$repositoryPackageCache'."
