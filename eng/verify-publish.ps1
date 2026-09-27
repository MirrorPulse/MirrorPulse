[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\MirrorPulse.Host\MirrorPulse.Host.csproj"
$publishRoot = Join-Path ([IO.Path]::GetTempPath()) "MirrorPulse-publish-$([guid]::NewGuid().ToString('N'))"
$runtimes = @("win-x64", "win-arm64")

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

foreach ($runtime in $runtimes) {
    $output = Join-Path $publishRoot $runtime
    & dotnet publish $project --configuration $Configuration --runtime $runtime --self-contained false --no-restore --output $output
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish for $runtime failed with exit code $LASTEXITCODE."
    }

    $hostAssembly = Join-Path $output "MirrorPulse.Host.dll"
    if (-not (Test-Path $hostAssembly -PathType Leaf)) {
        throw "The $runtime publish output does not contain MirrorPulse.Host.dll."
    }
}

Write-Output "Verified Host publishes for $($runtimes -join ', ')."
