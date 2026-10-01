# MirrorPulse

MirrorPulse is a Windows Cloud Files application that presents local and remote storage through one Explorer sync root. The project is built around independently packaged Adapter workers for local directories, WebDAV, SMB, FTP/FTPS, SFTP, and future providers.

## Repository scope

This repository contains the MirrorPulse application and its runtime code.

## Development status

The no-UI `mp` CLI and current-user Host control path run on x64 and ARM64. The CLI is also packaged as an MSIX app execution alias. Synchronization recovery, multi-root Worker routing, live instance changes, and parts of the control surface remain incomplete. See the [capability baseline](docs/cli-capabilities.md) for implemented, partial, and unsupported behavior, and the [CLI reference](docs/cli.md) for commands.

## Requirements

- A Microsoft-supported Windows 11 desktop release, version 24H2 (build 26100) or later. Windows 10 and Windows Server are excluded. See the [support matrix](docs/windows-support.md).
- .NET 10 SDK selected by `global.json`.
- x64 or ARM64.

## Build

```powershell
pwsh -File eng/setup-test-environment.ps1
dotnet restore MirrorPulse.sln
dotnet build MirrorPulse.sln --configuration Release
dotnet test MirrorPulse.sln --configuration Release
```

The setup script creates an isolated Python environment under ignored build
artifacts for the loopback SFTP tests. It does not install packages into global
Python. Pass `-Python <python.exe>` to select the base interpreter.

## Privacy

MirrorPulse keeps configuration, credentials, logs, and synchronization state on the user's device. It does not upload telemetry or diagnostics. Synchronization transfers files only to sources the user configures and authorizes.

## License

MirrorPulse is available under the [Apache License 2.0](LICENSE). See [NOTICE](NOTICE) for attribution information.
