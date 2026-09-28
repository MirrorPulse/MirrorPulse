[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
if (-not $IsWindows) {
    throw "Native Cloud Files verification requires Windows."
}

$testProject = Join-Path $PSScriptRoot "..\tests\MirrorPulse.CloudFiles.CfSharp.Tests\MirrorPulse.CloudFiles.CfSharp.Tests.csproj"
$previous = $env:MIRRORPULSE_NATIVE_TEST
try {
    $env:MIRRORPULSE_NATIVE_TEST = "1"
    & dotnet test $testProject --configuration Release --no-build --filter "FullyQualifiedName~Native"
    if ($LASTEXITCODE -ne 0) {
        throw "Native Cloud Files verification failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:MIRRORPULSE_NATIVE_TEST = $previous
}
