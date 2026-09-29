# CfSharp Preview Compatibility Report

MirrorPulse pins `CfSharp` and `CfSharp.Storage.Sqlite` to `0.1.0-preview.2` from
NuGet.org. The integration project owns CfSharp types and lifecycle; Core exposes
MirrorPulse contracts to Adapters and the product UI.

## Current boundary

| Area | Implemented behavior and evidence |
|---|---|
| Operating system | The Host requires Windows 10 version 2004 or later. CfSharp's broader platform minimum does not lower the product minimum. |
| Architectures | The Host publishes for `win-x64` and `win-arm64`. Native integration has been exercised locally on Windows x64; ARM64 runtime behavior remains unverified. |
| State | One official CfSharp SQLite database lives outside the single sync root. MirrorPulse keeps product catalog data separately and does not duplicate CfSharp's local journal, remote batches, conflicts, checkpoints, or echo records. |
| Registration | The Host starts a real CfSharp session with a current-user Shell and Cloud Files registration. Ordinary shutdown preserves registration and database state. |
| Demand | The integration provider maps bounded Adapter directory pages and range reads to CfSharp callbacks. A separate consumer process has enumerated an online-only placeholder, hydrated it, compared bytes, and read it after provider session restart. |
| Local changes | CfSharp's journal is the source for pending uploads, retry metadata, acknowledgement, and remote revision updates. |
| Remote changes | Adapter batches map to `CloudRemoteChangeBatch`; CfSharp owns application, partial progress, conflict IDs, echo suppression, and named checkpoints. |

The Host starts independent Worker EXEs for enabled instances and routes
directory pages, range reads, and supported local journal uploads through their
current-user Named Pipes. A signed Local Adapter release has been installed and
exercised with two simultaneous instances, separate first-level directories,
the CfSharp demand-provider boundary, range reads, and an upload. Shell registration in an
interactive installed MSIX session and full bidirectional polling remain open.

## Validation

The default CI gate runs locked restore, formatting, platform checks, Release
build and tests, and Host publishes for both architectures. On a Windows machine
with Cloud Files support, run `pwsh ./eng/verify-native.ps1` after the Release
build. The GitHub Actions workflow exposes this additional step through manual
dispatch. It is not part of pull-request CI because registration and external
consumer behavior need a suitable Windows user session.

The native set includes external-process directory enumeration and hydration,
official SQLite reopen, journal retry and acknowledgement, remote metadata echo
suppression, durable batch cursor behavior, and a second process that exits with
an open SQLite transaction. The recovery test checks committed journal, partial
remote batch, conflict, echo, and checkpoint records; it verifies the unfinished
transaction rolled back and `PRAGMA integrity_check` returned `ok`.

## Preview status

The earlier `CF-001`, `API-001`, and `API-002` gaps were retested after pinning
preview.2. MirrorPulse uses the public CfSharp batch and conflict contracts and
does not read or mutate CfSharp's private SQLite tables or conflict payload
format. Preview.2 remains a prerelease dependency until its final package is
published and validated.
