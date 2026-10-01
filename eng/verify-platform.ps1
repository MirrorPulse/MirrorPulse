[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$expectedTargetFramework = "net10.0-windows"
$specialTargetFrameworks = @{
    "MirrorPulse.App.csproj" = "net10.0-windows10.0.26100.0"
    "MirrorPulse.CloudFiles.CfSharp.csproj" = "net10.0-windows10.0.26100.0"
    "MirrorPulse.Host.csproj" = "net10.0-windows10.0.26100.0"
    "MirrorPulse.Core.Tests.csproj" = "net10.0-windows10.0.26100.0"
    "MirrorPulse.Cli.Tests.csproj" = "net10.0-windows10.0.26100.0"
    "MirrorPulse.CloudFiles.CfSharp.Tests.csproj" = "net10.0-windows10.0.26100.0"
    "MirrorPulse.CfSharp.CrashProbe.csproj" = "net10.0-windows10.0.26100.0"
}
$projectRoots = @("src", "tests", "Adapters")
$projects = foreach ($root in $projectRoots) {
    Get-ChildItem (Join-Path $repositoryRoot $root) -Filter "*.csproj" -Recurse -File
}

if ($projects.Count -eq 0) {
    throw "No project files were found under src or tests."
}

foreach ($project in $projects) {
    [xml]$projectFile = Get-Content $project.FullName -Raw
    $frameworks = @(
        $projectFile.Project.PropertyGroup |
            ForEach-Object { $_.TargetFramework } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )

    $expectedForProject = $expectedTargetFramework
    if ($specialTargetFrameworks.ContainsKey($project.Name)) {
        $expectedForProject = $specialTargetFrameworks[$project.Name]
    }

    if ($frameworks.Count -ne 1 -or $frameworks[0] -ne $expectedForProject) {
        throw "$($project.FullName) must target exactly $expectedForProject."
    }
}

[xml]$app = Get-Content -LiteralPath (Join-Path $repositoryRoot "src/MirrorPulse.App/MirrorPulse.App.csproj") -Raw
[xml]$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot "src/MirrorPulse.App/Package.appxmanifest") -Raw
$minimum = "10.0.26100.0"
$minima = @($app.SelectNodes("//TargetPlatformMinVersion"))
$families = @($manifest.Package.Dependencies.TargetDeviceFamily)
$policy = Get-Content -LiteralPath (Join-Path $repositoryRoot "src/MirrorPulse.Core/Configuration/MirrorPulsePlatform.cs") -Raw
if ($minima.Count -ne 1 -or $minima[0].InnerText -ne $minimum -or
    $families.Count -ne 1 -or $families[0].Name -ne "Windows.Desktop" -or
    $families[0].MinVersion -ne $minimum -or $families[0].MaxVersionTested -ne $minimum -or
    $policy -notmatch ('MinimumVersion\s*=\s*"' + [regex]::Escape($minimum) + '"')) {
    throw "Product runtime and Desktop MSIX minimum must agree on Windows 11 24H2 ($minimum)."
}
foreach ($name in @("MirrorPulse.App", "MirrorPulse.Host", "MirrorPulse.Cli")) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $repositoryRoot "src/$name/$name.csproj") -Raw
    $rids = @($project.SelectNodes("//RuntimeIdentifiers"))
    if ($rids.Count -ne 1 -or $rids[0].InnerText -ne "win-x64;win-arm64") {
        throw "$name must publish exactly win-x64 and win-arm64."
    }
}
Write-Output "Approved Windows frameworks, product minimum, Desktop family, and x64/ARM64 RIDs agree."
