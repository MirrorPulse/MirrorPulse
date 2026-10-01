[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceSha,
    [switch]$RequireNative,
    [switch]$RequireInstalled
)

$ErrorActionPreference = "Stop"
$expected = @{
    "build-and-test" = @{ runtime="win-x64";suite="managed" }
    "official-package-arm64" = @{ runtime="win-arm64";suite="signed" }
    "official-adapters" = @{ runtime="win-x64";suite="official" }
}
$manifests = @(Get-ChildItem -LiteralPath $EvidenceDirectory -File -Recurse -Filter "*.json" |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
if ($manifests.Count -ne 3) { throw "Exactly three CI job evidence manifests are required." }
foreach ($job in $expected.Keys) {
    $matches = @($manifests | Where-Object job -eq $job)
    if ($matches.Count -ne 1) { throw "Missing or duplicate CI job evidence: $job" }
    $manifest = $matches[0]
    if ($manifest.schemaVersion -ne 1 -or $manifest.sourceSha -cne $ExpectedSourceSha -or
        $manifest.runtime -ne $expected[$job].runtime -or $manifest.artifacts.Count -eq 0) {
        throw "CI evidence does not match the expected source/RID/artifacts: $job"
    }
    $suite = @($manifest.tests | Where-Object suite -eq $expected[$job].suite)
    if ($suite.Count -ne 1 -or $suite[0].executed -le 0 -or
        $suite[0].selected -ne ($suite[0].executed + $suite[0].skipped)) { throw "Required test execution is missing: $job" }
    if ($job -ne "build-and-test" -and $suite[0].skipped -ne 0) { throw "Dedicated tests were skipped: $job" }
    foreach ($artifact in $manifest.artifacts) {
        if ($artifact.sha256 -notmatch '^[0-9a-f]{64}$' -or $artifact.length -lt 0 -or
            $artifact.path -notmatch '^[A-Za-z0-9._/-]+$' -or $artifact.path.StartsWith('/') -or
            @($artifact.path.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
            throw "Invalid artifact evidence: $job"
        }
    }
    if ($job -eq "official-package-arm64" -and
        @($manifest.checks | Where-Object { $_.name -eq "signed-local-cli-regression" -and $_.executed -eq $true }).Count -ne 1) {
        throw "The ARM64 CLI regression did not execute."
    }
    if ($job -eq "build-and-test") {
        if ($RequireNative) {
            $native = @($manifest.tests | Where-Object suite -eq "native")
            if ($native.Count -ne 1 -or $native[0].skipped -ne 0 -or
                @($native[0].categories | Where-Object { $_.category -eq "native" -and $_.executed -eq 9 }).Count -ne 1) {
                throw "Native Cloud Files execution is missing."
            }
        }
        if ($RequireInstalled -and
            @($manifest.checks | Where-Object { $_.name -eq "installed-msix" -and $_.executed -eq $true -and $_.uninstalled -eq $true }).Count -ne 1) {
            throw "Installed MSIX verification is missing."
        }
    }
}
Write-Output "Verified all three CI job manifests for $ExpectedSourceSha."
