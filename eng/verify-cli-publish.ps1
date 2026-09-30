[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repositoryRoot "src\MirrorPulse.Cli\MirrorPulse.Cli.csproj"
$publishRoot = Join-Path ([IO.Path]::GetTempPath()) "MirrorPulse-cli-$Runtime-$([guid]::NewGuid().ToString('N'))"

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
& dotnet publish $project --configuration Release --runtime $Runtime --self-contained false --no-restore --output $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "CLI publish for $Runtime failed with exit code $LASTEXITCODE."
}

$executable = Join-Path $publishRoot "mp.exe"
if (-not (Test-Path $executable -PathType Leaf)) {
    throw "The $Runtime CLI publish output does not contain mp.exe."
}

$version = (& $executable --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $version -notmatch '^MirrorPulse mp [0-9]+\.[0-9]+\.[0-9]+') {
    throw "The $Runtime CLI did not return a valid version line."
}

Write-Output "Verified $Runtime CLI: $version"
