[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$runRoot = Join-Path ([IO.Path]::GetTempPath()) "MirrorPulse-cli-integration-$([guid]::NewGuid().ToString('N'))"
$cliRoot = Join-Path $runRoot "cli"
$hostRoot = Join-Path $runRoot "host"
$syncRoot = Join-Path $runRoot "sync"
$dataRoot = Join-Path $runRoot "data"
$sourceRoot = Join-Path $runRoot "source"
$hostProcess = $null

function Invoke-MirrorPulseCli {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $text = (& $script:cliExecutable @Arguments 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "CLI command failed ($LASTEXITCODE): $($Arguments -join ' ')`n$text"
    }

    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "CLI command returned no output: $($Arguments -join ' ')"
    }

    return $text | ConvertFrom-Json
}

try {
    if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        throw "The Adapter package was not found: $PackagePath"
    }

    New-Item -ItemType Directory -Path $cliRoot, $hostRoot, $syncRoot, $dataRoot, $sourceRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $sourceRoot "nested") -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $sourceRoot "nested\fixture.txt") -Value "MirrorPulse CLI fixture" -NoNewline

    $cliProject = Join-Path $repositoryRoot "src\MirrorPulse.Cli\MirrorPulse.Cli.csproj"
    $hostProject = Join-Path $repositoryRoot "src\MirrorPulse.Host\MirrorPulse.Host.csproj"
    & dotnet publish $cliProject --configuration $Configuration --runtime $Runtime --self-contained true --no-restore --output $cliRoot
    if ($LASTEXITCODE -ne 0) { throw "CLI publish failed." }
    & dotnet publish $hostProject --configuration $Configuration --runtime $Runtime --self-contained true --no-restore --output $hostRoot
    if ($LASTEXITCODE -ne 0) { throw "Host publish failed." }

    $script:cliExecutable = Join-Path $cliRoot "mp.exe"
    $hostExecutable = Join-Path $hostRoot "MirrorPulse.Host.exe"
    if (-not (Test-Path -LiteralPath $script:cliExecutable) -or
        -not (Test-Path -LiteralPath $hostExecutable)) {
        throw "The published CLI or Host executable is missing."
    }

    $env:MIRRORPULSE_DATA_ROOT = $dataRoot
    $env:MIRRORPULSE_SYNC_ROOT = $syncRoot
    $env:MIRRORPULSE_HOST_PATH = $hostExecutable
    $env:MIRRORPULSE_DEVELOPER_MODE = "1"

    $version = Invoke-MirrorPulseCli @("--json", "--version")
    if ($version.kind -ne "version") { throw "The CLI version envelope is invalid." }

    $hostStatus = Invoke-MirrorPulseCli @("--json", "--developer-mode", "host", "start")
    if ($hostStatus.kind -ne "result" -or $hostStatus.data.state -notin @("Running", "Degraded")) {
        throw "The Host did not reach a running state through the CLI."
    }

    $install = Invoke-MirrorPulseCli @("--json", "--developer-mode", "adapter", "install", "--package", (Resolve-Path $PackagePath).Path)
    $installId = $install.data.installedAdapterId
    if ([string]::IsNullOrWhiteSpace($installId)) { throw "The CLI install response did not contain an installation ID." }

    $instance = Invoke-MirrorPulseCli @("--json", "--developer-mode", "instance", "create",
        "--install-id", $installId, "--name", "CLI fixture",
        "--config", "sourceDirectory=$sourceRoot")
    $instanceId = $instance.data.createdInstanceId
    if ([string]::IsNullOrWhiteSpace($instanceId)) { throw "The CLI create response did not contain an instance ID." }

    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 500
        $status = Invoke-MirrorPulseCli @("--json", "--developer-mode", "status")
        $entry = @($status.data.instances) | Where-Object { $_.instanceId -eq $instanceId } | Select-Object -First 1
    } while ($null -eq $entry -and [DateTime]::UtcNow -lt $deadline)
    if ($null -eq $entry) { throw "The CLI status response did not contain the created instance." }

    $refresh = Invoke-MirrorPulseCli @("--json", "--developer-mode", "sync", "refresh")
    if ($refresh.kind -ne "result") { throw "The CLI refresh response is invalid." }

    $restart = Invoke-MirrorPulseCli @("--json", "--developer-mode", "host", "restart")
    if ($restart.kind -ne "result") { throw "The CLI Host restart response is invalid." }
    $listed = Invoke-MirrorPulseCli @("--json", "--developer-mode", "instance", "list")
    if (-not (@($listed.data.instances) | Where-Object { $_.instanceId -eq $instanceId })) {
        throw "The instance was not retained across a CLI Host restart."
    }

    Invoke-MirrorPulseCli @("--json", "--developer-mode", "host", "stop") | Out-Null
    Write-Output "Verified CLI -> Host -> Worker integration for $Runtime ($instanceId)."
}
finally {
    if ($null -ne $script:cliExecutable -and (Test-Path -LiteralPath $script:cliExecutable)) {
        try {
            & $script:cliExecutable --json --developer-mode host stop 2>$null | Out-Null
        } catch {
        }
    }
    if ($null -ne $hostProcess -and -not $hostProcess.HasExited) {
        $hostProcess.Kill($true)
    }
    Remove-Item Env:\MIRRORPULSE_DATA_ROOT -ErrorAction SilentlyContinue
    Remove-Item Env:\MIRRORPULSE_SYNC_ROOT -ErrorAction SilentlyContinue
    Remove-Item Env:\MIRRORPULSE_HOST_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:\MIRRORPULSE_DEVELOPER_MODE -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $runRoot) {
        Remove-Item -LiteralPath $runRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
