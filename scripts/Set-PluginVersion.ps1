[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$normalizedVersion = $Version.Trim()
if ($normalizedVersion.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
    $normalizedVersion = $normalizedVersion.Substring(1)
}

$parsed = $null
$core = ($normalizedVersion -split '[-+ ]', 2)[0]
if (-not [Version]::TryParse($core, [ref]$parsed)) {
    throw "Invalid release version '$Version'."
}

$projectPath = Join-Path $RepositoryRoot "LanDesktopHot.csproj"
$manifestPath = Join-Path $RepositoryRoot "plugin.json"
$readmePath = Join-Path $RepositoryRoot "README.md"

$project = [System.IO.File]::ReadAllText($projectPath)
$project = [System.Text.RegularExpressions.Regex]::Replace(
    $project,
    "<Version>.*?</Version>",
    "<Version>$normalizedVersion</Version>")
[System.IO.File]::WriteAllText($projectPath, $project, [System.Text.UTF8Encoding]::new($false))

$manifest = Get-Content $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
$manifest.version = $normalizedVersion
[System.IO.File]::WriteAllText(
    $manifestPath,
    (($manifest | ConvertTo-Json -Depth 20) + [Environment]::NewLine),
    [System.Text.UTF8Encoding]::new($false))

if (Test-Path $readmePath) {
    $readme = [System.IO.File]::ReadAllText($readmePath)
    $readme = [System.Text.RegularExpressions.Regex]::Replace(
        $readme,
        "LanDesktopHot\.\d+\.\d+\.\d+(?:[-+][A-Za-z0-9_.-]+)?\.laapp",
        "LanDesktopHot.$normalizedVersion.laapp")
    [System.IO.File]::WriteAllText($readmePath, $readme, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Set plugin version to $normalizedVersion."
