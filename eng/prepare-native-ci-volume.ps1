[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Native volume preparation is restricted to disposable GitHub-hosted runners.'
}
$volumes = @([IO.Path]::GetTempPath(), $env:RUNNER_TEMP) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($_)).TrimEnd('\') } |
    Sort-Object -Unique
foreach ($volume in $volumes) {
    if ([IO.DriveInfo]::new($volume).DriveFormat -ne 'NTFS') { throw 'The native fixture requires an NTFS test volume.' }
    & fsutil usn queryjournal $volume
    if ($LASTEXITCODE -ne 0) {
        & fsutil usn createjournal m=33554432 a=4194304 $volume
        if ($LASTEXITCODE -ne 0) { throw 'The disposable native test volume could not enable its USN journal.' }
        & fsutil usn queryjournal $volume
        if ($LASTEXITCODE -ne 0) { throw 'The disposable native test volume has no usable USN journal.' }
    }
}
