[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..' 'artifacts' 'official-adapters')
)

$ErrorActionPreference = 'Stop'

function Get-RequiredString {
    param(
        [Parameter(Mandatory)] [object]$Object,
        [Parameter(Mandatory)] [string]$Property,
        [Parameter(Mandatory)] [string]$Context
    )

    $value = $Object.$Property
    if ([string]::IsNullOrWhiteSpace([string]$value)) {
        throw "$Context is missing a non-empty '$Property' value."
    }

    return [string]$value
}

$lockPath = Join-Path $PSScriptRoot 'official-adapters.json'
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if ($lock.schemaVersion -ne 1 -or @($lock.adapters).Count -eq 0) {
    throw 'The official Adapter aggregation lock is invalid.'
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$headers = @{
    Accept = 'application/vnd.github+json'
    'User-Agent' = 'MirrorPulse-official-adapter-aggregator'
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    $headers.Authorization = "Bearer $env:GITHUB_TOKEN"
    $env:GH_TOKEN = $env:GITHUB_TOKEN
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$trustSourcePath = Join-Path $PSScriptRoot '..' 'src' 'MirrorPulse.Core' 'Security' 'MirrorPulseOfficialAdapterTrust.cs'
$trustSource = Get-Content -LiteralPath $trustSourcePath -Raw
$publicKeyMatch = [regex]::Match($trustSource, '(?s)-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----')
if (-not $publicKeyMatch.Success) {
    throw 'The built-in official Adapter trust anchor is missing its public key.'
}
$trustedKey = [Security.Cryptography.RSA]::Create()
$trustedKey.ImportFromPem($publicKeyMatch.Value)
$records = [System.Collections.Generic.List[object]]::new()
foreach ($entry in @($lock.adapters)) {
    $adapterId = Get-RequiredString $entry 'adapterId' 'Aggregation entry'
    $repository = Get-RequiredString $entry 'repository' "Aggregation entry '$adapterId'"
    if ($adapterId -notlike 'com.mirrorpulse.adapter.*' -or $repository -notmatch '^[^/]+/[^/]+$') {
        throw "Aggregation entry '$adapterId' has an invalid identity."
    }

    $release = (& gh api "repos/$repository/releases/latest" | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0 -or $null -eq $release) {
        throw "Unable to read the latest release for '$repository'."
    }
    $tag = Get-RequiredString $release 'tag_name' "Latest release for '$adapterId'"
    $version = $tag.TrimStart('v', 'V')
    $parsedVersion = $null
    if (-not [Version]::TryParse($version, [ref]$parsedVersion)) {
        throw "Latest release for '$adapterId' has a non-semantic tag '$tag'."
    }

    $assets = @($release.assets)
    $packages = @($assets | Where-Object { $_.name -match '\.mpadapter$' })
    if ($packages.Count -ne 1) {
        throw "Latest release for '$adapterId' must contain exactly one .mpadapter asset."
    }
    $packageAsset = $packages[0]
    $signatureName = "$($packageAsset.name).signature.json"
    $signatureAsset = @($assets | Where-Object { $_.name -ceq $signatureName })
    if ($signatureAsset.Count -ne 1) {
        throw "Latest release for '$adapterId' is missing its detached signature asset."
    }

    $adapterDirectory = Join-Path $OutputDirectory $adapterId
    New-Item -ItemType Directory -Path $adapterDirectory -Force | Out-Null
    $packagePath = Join-Path $adapterDirectory $packageAsset.name
    $signaturePath = Join-Path $adapterDirectory $signatureAsset[0].name
    & gh release download $tag --repo $repository --pattern $packageAsset.name --dir $adapterDirectory --clobber | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to download '$($packageAsset.name)' from '$repository'."
    }
    & gh release download $tag --repo $repository --pattern $signatureAsset[0].name --dir $adapterDirectory --clobber | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to download '$($signatureAsset[0].name)' from '$repository'."
    }

    $packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $packageLength = (Get-Item -LiteralPath $packagePath).Length
    $signature = Get-Content -LiteralPath $signaturePath -Raw | ConvertFrom-Json
    if ($signature.algorithm -ne 'RSA-SHA256' -or $signature.signer -ne 'MirrorPulse Team' -or
        [string]::IsNullOrWhiteSpace([string]$signature.signature) -or @($signature.files).Count -eq 0) {
        throw "The detached signature envelope for '$adapterId' is invalid."
    }
    $canonicalFiles = @($signature.files | Sort-Object path | ForEach-Object {
        [ordered]@{ path = $_.path; length = $_.length; sha256 = $_.sha256 }
    }) | ConvertTo-Json -Compress -Depth 5
    $signatureBytes = [Convert]::FromBase64String($signature.signature)
    if (-not $trustedKey.VerifyData(
            [Text.Encoding]::UTF8.GetBytes($canonicalFiles),
            $signatureBytes,
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)) {
        throw "The detached signature for '$adapterId' is not trusted by MirrorPulse."
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $manifestEntry = $archive.GetEntry('manifest.json')
        if ($null -eq $manifestEntry) {
            throw "The '$adapterId' package does not contain manifest.json."
        }
        $manifestStream = $manifestEntry.Open()
        try {
            $manifest = [System.Text.Json.JsonDocument]::Parse($manifestStream)
        }
        finally {
            $manifestStream.Dispose()
        }
        $manifestRoot = $manifest.RootElement
        $manifestAdapterId = $manifestRoot.GetProperty('adapterId').GetString()
        $manifestVersion = $manifestRoot.GetProperty('version').GetString()
        if ($manifestAdapterId -cne $adapterId -or $manifestVersion -cne $version) {
            throw "The '$adapterId' package manifest identity does not match its latest release tag."
        }
        foreach ($runtime in @('win-x64', 'win-arm64')) {
            $entryPoint = $manifestRoot.GetProperty('entrypoints').GetProperty($runtime).GetString()
            if ([string]::IsNullOrWhiteSpace($entryPoint) -or $null -eq $archive.GetEntry($entryPoint)) {
                throw "The '$adapterId' package is missing its $runtime Worker payload."
            }
        }
        $manifest.Dispose()
    }
    finally {
        $archive.Dispose()
    }

    [ordered]@{
        adapterId = $adapterId
        repository = $repository
        version = $version
        tag = $tag
        releaseUrl = $release.html_url
        package = $packageAsset.name
        packageLength = $packageLength
        packageSha256 = $packageHash
        signature = $signatureAsset[0].name
    } | ForEach-Object { $records.Add($_) }
}

$records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'official-adapters.manifest.json') -Encoding utf8
Write-Host "Aggregated $($records.Count) official Adapter releases into $OutputDirectory."
