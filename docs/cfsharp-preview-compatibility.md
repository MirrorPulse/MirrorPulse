# CfSharp Preview Compatibility Report

MirrorPulse pins `CfSharp` and `CfSharp.Storage.Sqlite` to `0.1.0-preview.1` from
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

The current Host starts with an empty Adapter provider. Independent Worker EXEs,
Named Pipe sessions, and the UI are not yet connected to this native session.
The directory and range integration tests use controlled sources to verify the
CfSharp boundary; they do not claim a complete product sync loop.

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

## Preview limitations

- **CF-001:** `DirectoryUpsert` for a new remote item with echo suppression
  returns `RequiresRetry` because the SQLite suppression row references an item
  that has not yet been inserted. The native test verifies the cursor does not
  advance on failure; successful new-directory partial replay still needs a
  corrected CfSharp preview.
- **API-001:** CfSharp exposes durable conflict IDs and opaque state payloads,
  but no public decoded conflict query. MirrorPulse keeps a non-authoritative UI
  projection and reports IDs whose details cannot be recovered after a crash gap.
- **API-002:** `KeepLocal` leaves a remote conflict unresolved in this preview.
  MirrorPulse reports that product action as unsupported instead of marking it
  resolved. A terminal keep-local decision requires a public CfSharp contract.

These limitations must be retested against a newer pinned package before the
affected workflows can be accepted. The integration does not read or mutate
CfSharp's private SQLite tables or conflict payload format.
