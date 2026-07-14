[CmdletBinding()]
param(
    [switch]$SkipLocalPackageFeed,
    [string]$LanMountainDesktopRoot,
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$repositoryPackageCache = [System.IO.Path]::GetFullPath((Join-Path $root ".nuget\packages"))
if (-not $repositoryPackageCache.StartsWith(
        $root + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe repository NuGet package cache path '$repositoryPackageCache'."
}

function Clear-RepositoryPackageCache {
    if (Test-Path -LiteralPath $repositoryPackageCache) {
        Remove-Item -LiteralPath $repositoryPackageCache -Recurse -Force
    }
}

function Test-RestoredDependencyGraph {
    $assetsPath = Join-Path $root "obj\project.assets.json"
    if (-not (Test-Path -LiteralPath $assetsPath)) {
        throw "Restore did not produce '$assetsPath'."
    }

    $assets = Get-Content $assetsPath -Encoding UTF8 -Raw | ConvertFrom-Json
    $libraries = @($assets.libraries.PSObject.Properties.Name)
    foreach ($requiredLibrary in @(
            "Avalonia/12.1.0",
            "FluentAvaloniaUI/3.0.1",
            "FluentIcons.Avalonia/2.1.331",
            "LanMountainDesktop.PluginSdk/5.0.0")) {
        if ($libraries -notcontains $requiredLibrary) {
            throw "Restored dependency graph is missing '$requiredLibrary'."
        }
    }

    $resolvedPackageFolders = @($assets.packageFolders.PSObject.Properties.Name) |
        ForEach-Object { ([string]$_).TrimEnd('\', '/') }
    $expectedPackageFolder = $repositoryPackageCache.TrimEnd('\', '/')
    if ($resolvedPackageFolders -notcontains $expectedPackageFolder) {
        throw "project.assets.json did not use repository package cache '$expectedPackageFolder'. Actual: $($resolvedPackageFolders -join ', ')"
    }
}

Push-Location $root
try {
    .\scripts\Test-PluginConsistency.ps1 -RepositoryRoot $root

    if (-not $SkipLocalPackageFeed) {
        if ([string]::IsNullOrWhiteSpace($LanMountainDesktopRoot)) {
            $LanMountainDesktopRoot = (Resolve-Path (Join-Path $root "..\LanMountainDesktop") -ErrorAction Stop).Path
        }

        .\scripts\Initialize-LocalPackageFeed.ps1 `
            -FeedPath (Join-Path $root "packages") `
            -PluginSdkProjectPath (Join-Path $LanMountainDesktopRoot "LanMountainDesktop.PluginSdk\LanMountainDesktop.PluginSdk.csproj") `
            -CoreContractsProjectPath (Join-Path $LanMountainDesktopRoot "LanMountainDesktop.Shared.Contracts\LanMountainDesktop.Shared.Contracts.csproj") `
            -PluginIsolationContractsProjectPath (Join-Path $LanMountainDesktopRoot "LanMountainDesktop.PluginIsolation.Contracts\LanMountainDesktop.PluginIsolation.Contracts.csproj") `
            -SharedIpcProjectPath (Join-Path $LanMountainDesktopRoot "LanMountainDesktop.Shared.IPC\LanMountainDesktop.Shared.IPC.csproj")
    }

    Clear-RepositoryPackageCache
    dotnet restore .\LanDesktopHot.csproj --configfile .\NuGet.config --packages $repositoryPackageCache --force --no-cache
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }
    Test-RestoredDependencyGraph

    dotnet build .\LanDesktopHot.csproj -c $Configuration --no-restore -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

    $manifest = Get-Content .\plugin.json -Encoding UTF8 -Raw | ConvertFrom-Json
    $packagePath = Join-Path $root "$($manifest.id).$($manifest.version).laapp"
    .\scripts\Test-PluginConsistency.ps1 -RepositoryRoot $root -PackagePath $packagePath

    $artifactsPath = Join-Path $root "artifacts"
    New-Item -ItemType Directory -Force -Path $artifactsPath | Out-Null
    $sha256 = (Get-FileHash -Path $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $md5 = (Get-FileHash -Path $packagePath -Algorithm MD5).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText(
        (Join-Path $artifactsPath "sha256.txt"),
        $sha256 + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText(
        (Join-Path $artifactsPath "md5.txt"),
        $md5 + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))

    .\scripts\New-MarketManifest.ps1 `
        -TemplatePath .\airappmarket-entry.template.json `
        -PackagePath $packagePath `
        -Version ([string]$manifest.version) `
        -ReleaseTag "v$($manifest.version)" `
        -OutputPath (Join-Path $artifactsPath "market-manifest.json")

    Write-Host "Package: $packagePath"
    Write-Host "Market manifest: $(Join-Path $artifactsPath "market-manifest.json")"
}
finally {
    Pop-Location
}
