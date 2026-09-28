[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SyncRootPath,

    [switch]$SkipLaunch
)

$ErrorActionPreference = "Stop"
$resolvedPath = [IO.Path]::GetFullPath($SyncRootPath.Trim())
if (-not (Test-Path -LiteralPath $resolvedPath -PathType Container)) {
    throw "The sync-root path does not exist: $resolvedPath"
}

if (-not $SkipLaunch) {
    Start-Process -FilePath "explorer.exe" -ArgumentList $resolvedPath | Out-Null
}

Write-Output "Explorer smoke target: $resolvedPath"
Write-Output "Manual checks:"
Write-Output "1. The MirrorPulse sync root opens in Explorer."
Write-Output "2. Adapter first-level directories are visible with their registered labels."
Write-Output "3. An online-only placeholder hydrates when opened."
Write-Output "4. A local edit appears in the upload queue and survives a Host restart."
Write-Output "5. A remote update reaches the placeholder without creating a provider echo."
