# Windows support

## Product policy

MirrorPulse supports Microsoft-supported Windows 11 desktop releases starting
with 24H2, build **10.0.26100.0**, on **x64 and ARM64**. This covers desktop Home,
Pro, Enterprise, and Education editions while their release remains in Microsoft
support. Install current Windows updates. Windows 10, Windows 11 releases before
24H2, Windows Server (including Desktop Experience), and x86/32-bit ARM are
excluded. Insider builds are development environments, not release qualification.

| Layer | Setting / behavior |
| --- | --- |
| SDK API target | Windows SDK 10.0.26100.0 for App, Host, and CfSharp integration |
| MSIX | Windows.Desktop only; MinVersion and MaxVersionTested 10.0.26100.0 |
| Runtime | Actual OS build >= 26100, workstation product type, x64 or ARM64 process |
| Distribution | Architecture-matched `win-x64` and `win-arm64` CLI/Host/MSIX |
| Servicing | Microsoft-supported desktop release and supported dependency patches |

The SDK target selects APIs available at compile time. Library API platform
annotations and dependencies' broader compatibility do not lower the product
minimum. MSIX's Desktop family can include Server; the runtime product-type
check explicitly excludes it. Unknown product types fail closed. This check uses
the local OS, without a network request or telemetry. It enforces the build/SKU/
architecture floor; it cannot determine an edition's changing servicing status
offline. A build number alone is not evidence of current Microsoft support.

CLI operations return exit **8**, JSON code `mp.platform.unsupported`, before
Host discovery, auto-start, or a control request. Help and version remain usable
for inspecting binaries. Direct Host startup returns exit 8 before opening state
or registering Cloud Files. WinUI shows the requirement and offers Close before
starting its normal client flow. Developer mode does not bypass the platform
check. MSIX deployment enforces MinVersion on older builds; unpackaged binaries
use the same runtime policy.

## Verification matrix

| Environment | Gate | What it establishes |
| --- | --- | --- |
| Windows Server x64 disposable CI runner | Published CLI/Host rejection; fixture roots stay absent | Actual unsupported-SKU refusal, exit/error contract, no product state creation |
| Windows 11 ARM64 disposable CI runner | Signed Local CLI regression and installed ARM64 MSIX | Supported desktop startup, native Worker, packaged identity, alias, Host auto-start, associations, uninstall |
| x64 and ARM64 publish | Locked restore, Release, executable metadata, hashed payload | Both distribution payloads build; metadata queries are independent of OS eligibility |
| Injected boundary cases | Platform policy and CLI entry tests | 26100 minimum, pre-24H2/Windows 10 refusal, server/domain-controller/unknown SKU refusal, architecture refusal, no Host call |

A manual CI run records the actual installed test OS version and product type.
The evidence collector refuses to accept a Server installation as supported
desktop evidence. `shellRegistration=false` means the Explorer interaction was
not tested. The legacy native filter includes eight Cloud Files integrations,
one official SQLite crash test, and five separately categorized managed helpers.

The first-release qualification matrix still needs clean native x64 desktop
installation and interactive Explorer checks across the supported OS/edition
matrix. Earlier Server MSIX installation reports are packaging history, not
desktop support qualification. Injected older-build tests are policy coverage,
not a claim that a Windows 10 VM deployment was executed.

## Dependency and runner references

Sources checked 2026-10-01:

- [.NET 10 supported operating systems](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
  lists Windows client architectures and servicing conditions.
- [Windows App SDK supported Windows releases](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/support)
  describes dependency support across client and Server releases.
- [Windows versioning overview](https://learn.microsoft.com/en-us/windows/apps/get-started/versioning-overview)
  separates OS version, SDK target, and API compatibility.
- [OSVERSIONINFOEXW](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-osversioninfoexw)
  defines the workstation/server product types.
- [GitHub-hosted runners](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
  documents the `windows-11-arm` desktop runner. `windows-latest` is used for
  Server build/rejection checks, not as Windows 11 installation evidence.
