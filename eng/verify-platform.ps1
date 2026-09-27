[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$expectedTargetFramework = "net10.0-windows"
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

    if ($frameworks.Count -ne 1 -or $frameworks[0] -ne $expectedTargetFramework) {
        throw "$($project.FullName) must target exactly $expectedTargetFramework."
    }
}

Write-Output "All projects target $expectedTargetFramework."
