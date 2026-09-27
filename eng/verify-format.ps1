[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$solution = Join-Path $PSScriptRoot "..\MirrorPulse.sln"

& dotnet format $solution --verify-no-changes --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "dotnet format verification failed with exit code $LASTEXITCODE."
}
