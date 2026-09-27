[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$expectedTargetFramework = "net10.0-windows"
$specialTargetFrameworks = @{
    "MirrorPulse.App.csproj" = "net10.0-windows10.0.26100.0"
}
$projectRoots = @("src", "tests")
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

Write-Output "All projects target their approved Windows frameworks."
