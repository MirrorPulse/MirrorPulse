[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$EvidencePath
)

$ErrorActionPreference = "Stop"
$os = Get-CimInstance -ClassName Win32_OperatingSystem
$version = [version]$os.Version
$osVersion = [version]::new($version.Major, $version.Minor, $version.Build, 0).ToString()
if ($os.ProductType -notin @(2, 3)) { throw "This rejection gate requires a disposable Windows Server runner." }
$fixture = Join-Path ([IO.Path]::GetTempPath()) "MirrorPulse-platform-$([guid]::NewGuid().ToString('N'))"
function Invoke-RejectedProcess([string]$Executable, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment["MIRRORPULSE_DATA_ROOT"] = Join-Path $fixture "data"
    $start.Environment["MIRRORPULSE_SYNC_ROOT"] = Join-Path $fixture "sync"
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(20000)) {
            $process.Kill($true)
            throw "An unsupported product process did not reject the platform promptly."
        }
        if ($process.ExitCode -ne 8 -or $stdout.Result.Trim().Length -ne 0 -or
            $stderr.Result -notmatch 'Windows Server are not supported') {
            throw "The unsupported platform was not rejected with exit 8 and a safe diagnostic."
        }
        $stderr.Result
    }
    finally { $process.Dispose() }
}
$cliError = Invoke-RejectedProcess (Join-Path $PublishDirectory "mp.exe") @("--json", "--no-start", "status")
if (($cliError | ConvertFrom-Json).code -ne "mp.platform.unsupported") { throw "The CLI JSON rejection code is incorrect." }
Invoke-RejectedProcess (Join-Path $PublishDirectory "host/MirrorPulse.Host.exe") @("--run-once") | Out-Null
if (Test-Path -LiteralPath $fixture) { throw "An unsupported process created product state or a sync root." }
[ordered]@{
    schemaVersion=1;runtime="win-x64";osVersion=$osVersion;osProductType=[int]$os.ProductType
    cliRejected=$true;hostRejected=$true;stateUntouched=$true
} | ConvertTo-Json | Set-Content -LiteralPath $EvidencePath -Encoding utf8
Write-Output "Verified CLI/Host rejection on Windows Server without product state creation."
