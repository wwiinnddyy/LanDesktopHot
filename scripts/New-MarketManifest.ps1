[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TemplatePath,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$ReleaseTag,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-PropertyValue($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-ArrayValue($Object, [string]$Name) {
    $value = Get-PropertyValue -Object $Object -Name $Name
    if ($null -eq $value) { return @() }
    if ($value -is [array]) { return $value }
    return @($value)
}

function Get-PackageManifest([string]$ArchivePath) {
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

function Get-RepositoryInfo([string]$RepositoryUrl) {
    $uri = [Uri]$RepositoryUrl
    if ($uri.Host -ne "github.com") {
        throw "Unsupported repository host in '$RepositoryUrl'."
    }

    $segments = $uri.AbsolutePath.Trim("/") -split "/"
    if ($segments.Length -ne 2) {
        throw "Repository URL '$RepositoryUrl' must point to a GitHub repository root."
    }

    return @{ Owner = $segments[0]; Name = $segments[1] }
}

$template = Get-Content $TemplatePath -Encoding UTF8 -Raw | ConvertFrom-Json
$resolvedPackagePath = (Resolve-Path $PackagePath -ErrorAction Stop).Path
$manifest = Get-PackageManifest -ArchivePath $resolvedPackagePath

if ($manifest.version -ne $Version) {
    throw "Requested version '$Version' does not match package manifest version '$($manifest.version)'."
}

if ($ReleaseTag -ne "v$Version") {
    throw "Release tag '$ReleaseTag' must be v$Version."
}

$assetName = [System.IO.Path]::GetFileName($resolvedPackagePath)
$expectedAssetName = "$($manifest.id).$Version.laapp"
if ($assetName -ne $expectedAssetName) {
    throw "Package name mismatch. Expected '$expectedAssetName', actual '$assetName'."
}

$repositoryUrl = [string](Get-PropertyValue $template "repositoryUrl")
$repo = Get-RepositoryInfo -RepositoryUrl $repositoryUrl
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY) -and $env:GITHUB_REPOSITORY -match "^[^/]+/[^/]+$") {
    $parts = $env:GITHUB_REPOSITORY -split "/", 2
    $repo.Owner = $parts[0]
    $repo.Name = $parts[1]
}

$actualRepositoryUrl = "https://github.com/$($repo.Owner)/$($repo.Name)"
$hash = (Get-FileHash -Path $resolvedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item $resolvedPackagePath).Length
$tags = @(Get-ArrayValue $template "tags")
$components = @(Get-ArrayValue $template "desktopComponents")
$settingsSections = @(Get-ArrayValue $template "settingsSections")
$releaseUrl = "$actualRepositoryUrl/releases/download/$ReleaseTag/$assetName"

$entry = [pscustomobject][ordered]@{
    schemaVersion = "2.0.0"
    generatedAt = [DateTimeOffset]::UtcNow.ToString("o")
    manifest = [pscustomobject][ordered]@{
        id = [string]$manifest.id
        name = [string]$manifest.name
        description = [string]$manifest.description
        author = [string]$manifest.author
        version = [string]$manifest.version
        apiVersion = [string]$manifest.apiVersion
        entranceAssembly = [string]$manifest.entranceAssembly
        sharedContracts = @($manifest.sharedContracts)
        runtime = $manifest.runtime
        tags = $tags
        releaseTag = $ReleaseTag
        releaseAssetName = $assetName
        releaseNotes = [string]$template.releaseNotes
    }
    compatibility = [pscustomobject][ordered]@{
        minHostVersion = [string]$template.minHostVersion
        apiVersion = [string]$manifest.apiVersion
        sharedContracts = @($manifest.sharedContracts)
    }
    repository = [pscustomobject][ordered]@{
        projectUrl = $actualRepositoryUrl
        readmeUrl = "$actualRepositoryUrl#readme"
        homepageUrl = [string]$template.homepageUrl
        repositoryUrl = $actualRepositoryUrl
        iconUrl = [string]$template.iconUrl
        tags = $tags
        releaseNotes = [string]$template.releaseNotes
    }
    publication = [pscustomobject][ordered]@{
        releaseTag = $ReleaseTag
        releaseAssetName = $assetName
        downloadUrl = $releaseUrl
        sha256 = $hash
        packageSizeBytes = $size
        packageSources = @(
            [pscustomobject][ordered]@{ kind = "releaseAsset"; url = $releaseUrl; assetName = $assetName; sha256 = $hash; sizeBytes = $size; releaseTag = $ReleaseTag; priority = 0 },
            [pscustomobject][ordered]@{ kind = "rawFallback"; url = "https://raw.githubusercontent.com/$($repo.Owner)/$($repo.Name)/main/$assetName"; assetName = $assetName; sha256 = $hash; sizeBytes = $size; releaseTag = $ReleaseTag; priority = 1 },
            [pscustomobject][ordered]@{ kind = "workspaceLocal"; path = "workspace://$($repo.Name)/$assetName"; assetName = $assetName; sha256 = $hash; sizeBytes = $size; releaseTag = $ReleaseTag; priority = 2 }
        )
    }
    capabilities = [pscustomobject][ordered]@{
        sharedContracts = @($manifest.sharedContracts)
        desktopComponents = $components
        settingsSections = $settingsSections
        exports = @()
        messageTypes = @()
    }
}

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$encoding = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($OutputPath, (($entry | ConvertTo-Json -Depth 30) + [Environment]::NewLine), $encoding)
Write-Host "Generated market manifest at '$OutputPath'."
