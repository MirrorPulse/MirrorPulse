# CfSharp Preview Compatibility Report

Status: verified against `CfSharp` `0.1.0-preview.1` from the public NuGet feed.

## Supported boundary

| Area | Compatibility decision |
|---|---|
| Operating system | Windows 10 version 1709 or later, subject to Cloud Files availability |
| Process architectures | `win-x64` and `win-arm64` |
| Package source | NuGet.org |
| Package lock | Core, Host, and Core.Tests lock files record the public package hashes |
| State ownership | CfSharp receives MP-owned durable state adapters; credentials and content remain outside the CfSharp state boundary |

## API surface exercised by MirrorPulse

| CfSharp surface | MirrorPulse boundary | Validation |
|---|---|---|
| `CloudSyncRoot.Register` and `Open` | Sync-root registration and account removal | Core unit tests and Windows publish gate |
| `CloudFileSystem` | Host lifecycle and remote change application | Core builder and remote batch tests |
| `CloudDirectory.CreatePlaceholdersAsync` | Remote directory placeholder planning | Placeholder batch tests |
| `ICloudFileContentProvider` | Hydration demand and Worker range reads | Content bridge and hydration callback tests |
| `CloudLocalChangeFeed` | Local feed startup, rescan acknowledgement, and echo suppression | Feed/session and full-rescan tests |
| `ApplyRemoteChangesAsync` | Ordered remote batch application | Remote batch applier tests |
| Durable state contracts | Remote cursors, recovery checkpoints, and request fingerprints | State-store round-trip tests |

## Evidence

- `dotnet restore MirrorPulse.sln --locked-mode`
- `dotnet format MirrorPulse.sln --verify-no-changes --no-restore`
- `dotnet test MirrorPulse.sln --configuration Release --no-build`
- Host publish checks for `win-x64` and `win-arm64`
- Windows Cloud Files integration harness and Explorer smoke checklist are available for manual validation.

## Limitations

This report covers the pinned preview API surface used by the current code. Native Explorer behavior, hydration timing, and provider recovery still require a Windows Cloud Files environment. A suspected preview defect must include a reproduction, expected and actual behavior, native error details, impact, workaround, and status.
