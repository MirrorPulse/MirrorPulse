# MirrorPulse

MirrorPulse is a Windows Cloud Files application that presents local and remote storage through one Explorer sync root. The project is built around independently packaged Adapter workers for local directories, WebDAV, SMB, FTP/FTPS, SFTP, and future providers.

## Repository scope

This repository contains the MirrorPulse application and its runtime code. Planning notes and collaboration records live in the ignored `draft/` directory.

## Development status

The repository is being built from the project contracts in `draft/需求文档.md` and `draft/项目书.md`. The current branch establishes the English codebase baseline before runtime features are added.

## Requirements

- Windows 10 version 1709 or later; exact supported builds are validated by the support matrix.
- .NET 10 SDK selected by `global.json`.
- x64 or ARM64.

## Build

```powershell
dotnet restore MirrorPulse.sln
dotnet build MirrorPulse.sln --configuration Release
dotnet test MirrorPulse.sln --configuration Release
```

## Privacy

MirrorPulse is designed to keep configuration, credentials, logs, and synchronization state on the user's device. It does not upload telemetry or user files by default.
