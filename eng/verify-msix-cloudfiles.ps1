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
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=AppPublisher" `
        -CertStoreLocation "Cert:\CurrentUser\My" -HashAlgorithm SHA256 -KeyLength 2048
    $pfxPath = Join-Path $packageRoot "MirrorPulse.TestSigning.pfx"
    $cerPath = Join-Path $packageRoot "MirrorPulse.TestSigning.cer"
    $passwordText = [guid]::NewGuid().ToString('N')
    $password = ConvertTo-SecureString -String $passwordText -AsPlainText -Force
    Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $password | Out-Null
    Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null
    # Keep the verification self-contained for a non-administrator CI runner. A
    # package signed by a certificate trusted by the current user is sufficient
    # for Add-AppxPackage and for app-execution-alias registration.
    Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
    Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null

    & dotnet build $project --configuration $Configuration --runtime win-x64 `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageSigningEnabled=true `
        -p:PackageCertificateThumbprint=$($certificate.Thumbprint) `
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

    Add-Type -AssemblyName System.IO.Compression
    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $packageEntries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('/', '\') })
        if ($packageEntries -notcontains "mp.exe") {
            throw "The MSIX payload does not contain the root-level mp.exe CLI alias target."
        }
        if ($packageEntries -notcontains "host\MirrorPulse.Host.exe") {
            throw "The MSIX payload does not contain host\MirrorPulse.Host.exe."
        }
    }
    finally {
        $archive.Dispose()
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
    $namespace.AddNamespace("uap3", "http://schemas.microsoft.com/appx/manifest/uap/windows10/3")
    $namespace.AddNamespace("desktop", "http://schemas.microsoft.com/appx/manifest/desktop/windows10")
    $namespace.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")
    $cloudFiles = $manifest.SelectSingleNode(
        "/f:Package/f:Applications/f:Application/f:Extensions/desktop3:Extension[@Category='windows.cloudFiles']",
        $namespace)
    if ($null -eq $cloudFiles) {
        throw "The installed MSIX identity does not expose the windows.cloudFiles extension."
    }
    $adapterAssociation = $manifest.SelectSingleNode(
        "/f:Package/f:Applications/f:Application/f:Extensions/uap:Extension[@Category='windows.fileTypeAssociation']/uap:FileTypeAssociation[uap:SupportedFileTypes/uap:FileType='.mpadapter']",
        $namespace)
    if ($null -eq $adapterAssociation) {
        throw "The installed MSIX does not associate .mpadapter files."
    }
    $executionAlias = $manifest.SelectSingleNode(
        "/f:Package/f:Applications/f:Application/f:Extensions/uap3:Extension[@Category='windows.appExecutionAlias']/uap3:AppExecutionAlias/desktop:ExecutionAlias[@Alias='mp.exe']",
        $namespace)
    if ($null -eq $executionAlias) {
        throw "The installed MSIX does not expose the mp.exe app execution alias."
    }

    $mpCommand = Get-Command mp.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $mpCommand) {
        throw "The installed MSIX did not register the mp.exe app execution alias."
    }
    $versionOutput = (& $mpCommand.Source --version 2>&1 | Out-String).Trim()
    if ($versionOutput -notmatch "MirrorPulse mp") {
        throw "The registered mp.exe alias did not return the CLI version: $versionOutput"
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

    Write-Output "Verified installed MSIX $($installed.PackageFullName) Cloud Files and .mpadapter identities."
}
finally {
    if ($null -ne $installed) {
        Remove-AppxPackage -Package $installed.PackageFullName -ErrorAction SilentlyContinue
    }
    if ($null -ne $certificate) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath "Cert:\CurrentUser\TrustedPeople\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath "Cert:\CurrentUser\Root\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $packageRoot) {
        Remove-Item -LiteralPath $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
