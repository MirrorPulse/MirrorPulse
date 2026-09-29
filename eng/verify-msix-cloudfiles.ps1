[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$VerifyShell
)

$ErrorActionPreference = "Stop"
if (-not $IsWindows) {
    throw "MSIX Cloud Files verification requires Windows."
}

$project = Join-Path $PSScriptRoot "..\src\MirrorPulse.App\MirrorPulse.App.csproj"
$packageRoot = Join-Path ([IO.Path]::GetTempPath()) "MirrorPulse-msix-$([guid]::NewGuid().ToString('N'))"
$certificate = $null
$package = $null

try {
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    $certificate = New-SelfSignedCertificate -Type Custom -Subject "CN=AppPublisher" `
        -KeyUsage DigitalSignature -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    $pfxPath = Join-Path $packageRoot "MirrorPulse.TestSigning.pfx"
    $cerPath = Join-Path $packageRoot "MirrorPulse.TestSigning.cer"
    $passwordText = [guid]::NewGuid().ToString('N')
    $password = ConvertTo-SecureString -String $passwordText -AsPlainText -Force
    Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $password | Out-Null
    Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null
    Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null

    & dotnet build $project --configuration $Configuration --runtime win-x64 `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageSigningEnabled=true `
        -p:PackageCertificateKeyFile=$pfxPath `
        -p:PackageCertificatePassword=$passwordText `
        -p:AppxPackageDir="$packageRoot\" `
        -p:MirrorPulseShellProbe=$($VerifyShell.IsPresent.ToString().ToLowerInvariant()) `
        --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "The packaged MirrorPulse.App build failed with exit code $LASTEXITCODE."
    }

    $package = Get-ChildItem -LiteralPath $packageRoot -Filter "*.msix" -File -Recurse |
        Select-Object -First 1
    if ($null -eq $package) {
        throw "The packaged build did not produce an MSIX file."
    }

    Add-AppxPackage -Path $package.FullName -ForceApplicationShutdown
    $installed = Get-AppxPackage -Name "0B72358D-6DC9-479D-8C28-F0232B42A0B3" |
        Select-Object -First 1
    if ($null -eq $installed) {
        throw "The test MSIX package was not installed for the current user."
    }

    [xml]$manifest = Get-AppxPackageManifest -Package $installed.PackageFullName
    $namespace = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $namespace.AddNamespace("f", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
    $namespace.AddNamespace("desktop3", "http://schemas.microsoft.com/appx/manifest/desktop/windows10/3")
    $cloudFiles = $manifest.SelectSingleNode(
        "/f:Package/f:Applications/f:Application/f:Extensions/desktop3:Extension[@Category='windows.cloudFiles']",
        $namespace)
    if ($null -eq $cloudFiles) {
        throw "The installed MSIX identity does not expose the windows.cloudFiles extension."
    }

    if ($VerifyShell) {
        $resultPath = Join-Path $packageRoot "packaged-shell-probe.result"
        $appExe = Join-Path $installed.InstallLocation "MirrorPulse.App.exe"
        if (-not (Test-Path -LiteralPath $appExe)) {
            throw "The installed MirrorPulse app executable was not found."
        }

        $env:MIRRORPULSE_PACKAGED_SHELL_PROBE_RESULT = $resultPath
        try {
            $probeProcess = Start-Process -FilePath $appExe -WindowStyle Hidden -PassThru
            $deadline = [DateTime]::UtcNow.AddMinutes(2)
            while (-not (Test-Path -LiteralPath $resultPath) -and [DateTime]::UtcNow -lt $deadline) {
                if ($probeProcess.HasExited) { break }
                Start-Sleep -Milliseconds 500
            }
        }
        finally {
            Remove-Item Env:\MIRRORPULSE_PACKAGED_SHELL_PROBE_RESULT -ErrorAction SilentlyContinue
        }

        if (-not (Test-Path -LiteralPath $resultPath)) {
            throw "The packaged App process did not report a Shell probe result."
        }

        $probeResult = (Get-Content -LiteralPath $resultPath -Raw).Trim()
        if ($probeResult -ne "success") {
            throw "The packaged App Shell probe failed: $probeResult"
        }

        Write-Output "Verified packaged Shell registration for $($installed.PackageFullName)."
    }

    Write-Output "Verified installed MSIX $($installed.PackageFullName) Cloud Files extension identity."
}
finally {
    if ($null -ne $installed) {
        Remove-AppxPackage -Package $installed.PackageFullName -ErrorAction SilentlyContinue
    }
    if ($null -ne $certificate) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $packageRoot) {
        Remove-Item -LiteralPath $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
