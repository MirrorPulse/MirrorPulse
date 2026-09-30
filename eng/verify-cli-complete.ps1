[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$schemaPath = Join-Path $repositoryRoot "src\MirrorPulse.Control\Contracts\ControlSchema.cs"
$parserPath = Join-Path $repositoryRoot "src\MirrorPulse.Cli\MirrorPulseCliCommandLine.cs"
$helpPath = Join-Path $repositoryRoot "docs\cli.md"
$matrixPath = Join-Path $repositoryRoot "docs\cli-capabilities.md"

foreach ($path in @($schemaPath, $parserPath, $helpPath, $matrixPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "CLI completion input is missing: $path"
    }
}

$schema = Get-Content -LiteralPath $schemaPath -Raw
if ($schema -notmatch 'CurrentVersion\s*=\s*1\b') {
    throw "The Control protocol is not frozen at schema version 1."
}

$parser = Get-Content -LiteralPath $parserPath -Raw
$requiredCommands = @(
    '"status"', '"sync"', '"adapter"', '"instance"',
    '"conflict"', '"operation"', '"config"', '"diagnostics"',
    '"developer-mode"', '"startup"', '"host"'
)
foreach ($token in $requiredCommands) {
    if ($parser.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "CLI parser is missing the required command token $token."
    }
}

$cliOutput = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "src\MirrorPulse.Cli\bin\$Configuration") `
    -Filter "mp.exe" -File -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $cliOutput) {
    throw "The built CLI executable was not found under src/MirrorPulse.Cli/bin/$Configuration."
}
$help = & $cliOutput.FullName --help 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    throw "The built CLI help command failed with exit code $LASTEXITCODE."
}
foreach ($token in @("adapter", "instance", "conflict", "operation", "diagnostics", "developer-mode", "startup")) {
    if ($help -notmatch [Regex]::Escape($token)) {
        throw "CLI help does not expose '$token'."
    }
}

Write-Output "CLI completion gate passed: Control schema 1, command catalog, documentation, and built help are aligned."
