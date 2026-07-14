[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$PackagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
}

function Get-Version([string]$Value) {
    $candidate = $Value.Trim()
    if ($candidate.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $candidate = $candidate.Substring(1)
    }

    $parsed = $null
    $core = ($candidate -split '[-+ ]', 2)[0]
    if (-not [Version]::TryParse($core, [ref]$parsed)) {
        throw "Invalid version '$Value'."
    }

    return $candidate
}

function Get-ManifestFromPackage([string]$ArchivePath) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -eq "plugin.json" } | Select-Object -First 1
        if ($null -eq $entry) {
            throw "Plugin package '$ArchivePath' does not contain plugin.json."
        }

        $stream = $entry.Open()
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.UTF8Encoding]::UTF8, $true)
        try {
            return $reader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

$projectPath = Join-Path $RepositoryRoot "LanDesktopHot.csproj"
$manifestPath = Join-Path $RepositoryRoot "plugin.json"
$templatePath = Join-Path $RepositoryRoot "airappmarket-entry.template.json"
$pluginSourcePath = Join-Path $RepositoryRoot "Plugin.cs"
$nugetConfigPath = Join-Path $RepositoryRoot "NuGet.config"
$gitIgnorePath = Join-Path $RepositoryRoot ".gitignore"
$buildScriptPath = Join-Path $RepositoryRoot "scripts\build-local.ps1"

foreach ($requiredPath in @($projectPath, $manifestPath, $templatePath, $pluginSourcePath, $nugetConfigPath, $gitIgnorePath, $buildScriptPath)) {
    if (-not (Test-Path $requiredPath)) {
        throw "Missing required file '$requiredPath'."
    }
}

[xml]$nugetConfig = Get-Content $nugetConfigPath -Encoding UTF8 -Raw
$globalPackagesFolder = $nugetConfig.configuration.config.add |
    Where-Object { $_.key -eq "globalPackagesFolder" } |
    Select-Object -First 1
if ($null -eq $globalPackagesFolder -or $globalPackagesFolder.value -ne ".nuget/packages") {
    throw "NuGet.config must set globalPackagesFolder to .nuget/packages."
}
if ((Get-Content $gitIgnorePath -Encoding UTF8 -Raw) -notmatch '(?m)^\.nuget/$') {
    throw ".gitignore must ignore .nuget/."
}
$buildScriptText = Get-Content $buildScriptPath -Encoding UTF8 -Raw
foreach ($restoreFlag in @("--force", "--no-cache", "--packages")) {
    if (-not $buildScriptText.Contains($restoreFlag)) {
        throw "build-local.ps1 restore must include '$restoreFlag'."
    }
}

$projectText = [System.IO.File]::ReadAllText($projectPath)
$versionMatch = [System.Text.RegularExpressions.Regex]::Match(
    $projectText,
    "<Version>(?<version>.*?)</Version>",
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $versionMatch.Success) {
    throw "Missing <Version> in '$projectPath'."
}

if ($projectText -notmatch '<PackageReference\s+Include="LanMountainDesktop\.PluginSdk"\s+Version="5\.0\.0"') {
    throw "LanDesktopHot.csproj must reference LanMountainDesktop.PluginSdk 5.0.0."
}
if ($projectText -notmatch '<RestorePackagesPath>\$\(MSBuildProjectDirectory\)\\\.nuget\\packages</RestorePackagesPath>') {
    throw "LanDesktopHot.csproj must pin RestorePackagesPath to the repository .nuget/packages directory."
}

if ($projectText -match 'LanMountainDesktop\.AirAppSdk') {
    throw "Production Plugin SDK projects must not reference LanMountainDesktop.AirAppSdk."
}

$legacySource = Get-ChildItem -Path $RepositoryRoot -Recurse -File -Include *.cs,*.csproj |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Where-Object { [System.IO.File]::ReadAllText($_.FullName) -match 'LanMountainDesktop\.AirAppSdk|AirAppBase|AirAppEntrance|AirAppWidgetBase' }
if ($legacySource) {
    throw "Legacy AirApp API remains in: $($legacySource.FullName -join ', ')"
}

if (Test-Path (Join-Path $RepositoryRoot "airapp.json")) {
    throw "Remove legacy airapp.json; Plugin SDK 5 packages use plugin.json only."
}

$projectVersion = Get-Version $versionMatch.Groups["version"].Value
$manifest = Get-Content $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
$manifestVersion = Get-Version ([string]$manifest.version)
if ($projectVersion -ne $manifestVersion) {
    throw "Version mismatch. csproj=$projectVersion plugin.json=$manifestVersion"
}

if ($manifest.id -ne "LanDesktopHot") {
    throw "Plugin id mismatch. Expected LanDesktopHot, actual=$($manifest.id)"
}

if ($manifest.apiVersion -ne "5.0.0") {
    throw "plugin.json apiVersion must be 5.0.0."
}

if ($manifest.entranceAssembly -ne "LanDesktopHot.dll") {
    throw "Entrance assembly mismatch. Expected LanDesktopHot.dll, actual=$($manifest.entranceAssembly)"
}

if ($manifest.runtime.mode -ne "in-proc") {
    throw "Runtime mode mismatch. Expected in-proc, actual=$($manifest.runtime.mode)"
}

$pluginSource = [System.IO.File]::ReadAllText($pluginSourcePath)
foreach ($token in @("[PluginEntrance]", "PluginBase", "AddPluginDesktopComponent<Widgets.ZhihuHotListWidget>")) {
    if (-not $pluginSource.Contains($token)) {
        throw "Plugin registration source is missing '$token'."
    }
}

$template = Get-Content $templatePath -Encoding UTF8 -Raw | ConvertFrom-Json
if ($template.minHostVersion -ne "0.8.6") {
    throw "Plugin SDK 5 first shipped with host 0.8.6; market template minHostVersion must be 0.8.6."
}
if (@($template.desktopComponents) -notcontains "zhihu-hotlist") {
    throw "Market template must declare desktop component 'zhihu-hotlist'."
}
if (@($template.settingsSections).Count -ne 0) {
    throw "Market template settingsSections must be empty."
}

$expectedAssetName = "$($manifest.id).$projectVersion.laapp"
if ($PackagePath) {
    $resolvedPackagePath = (Resolve-Path $PackagePath -ErrorAction Stop).Path
    if ([System.IO.Path]::GetFileName($resolvedPackagePath) -ne $expectedAssetName) {
        throw "Package name mismatch. Expected '$expectedAssetName', actual '$([System.IO.Path]::GetFileName($resolvedPackagePath))'."
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object FullName)
        foreach ($requiredEntry in @("plugin.json", "LanDesktopHot.dll", "LanDesktopHot.deps.json")) {
            if ($entryNames -notcontains $requiredEntry) {
                throw "Package is missing '$requiredEntry'."
            }
        }
        if ($entryNames -contains "airapp.json") {
            throw "Package must not contain legacy airapp.json."
        }
        if ($entryNames -contains "LanMountainDesktop.PluginSdk.dll") {
            throw "Package must not bundle host-provided LanMountainDesktop.PluginSdk.dll."
        }
    }
    finally {
        $archive.Dispose()
    }

    $packageManifest = Get-ManifestFromPackage -ArchivePath $resolvedPackagePath
    if ($packageManifest.id -ne $manifest.id -or
        $packageManifest.version -ne $manifest.version -or
        $packageManifest.apiVersion -ne $manifest.apiVersion -or
        $packageManifest.runtime.mode -ne $manifest.runtime.mode) {
        throw "Package manifest does not match repository plugin.json."
    }
}

Write-Host "Plugin version: $projectVersion"
Write-Host "Plugin API version: $($manifest.apiVersion)"
Write-Host "Expected asset: $expectedAssetName"
