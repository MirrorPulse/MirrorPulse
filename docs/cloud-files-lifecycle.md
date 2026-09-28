# Cloud Files Lifecycle

This document describes the lifecycle boundary between the MirrorPulse Host, CfSharp, and Adapter Workers.

## Startup

1. The Host acquires the current-user owner lock.
2. The Host loads the persisted registration, recovery checkpoint, remote cursor, and enabled Adapter instances.
3. The Host probes the Windows and process architecture boundary before opening native Cloud Files resources.
4. The Host opens or registers the `MirrorPulse` sync root and creates the CfSharp `CloudFileSystem`.
5. Each enabled instance starts its Worker and establishes the named-pipe session.
6. The Host starts the local change feed and begins remote enumeration from the persisted cursor.

Disabled instances retain their configuration and placeholders but do not start a Worker or network operation.

## Steady state

- Hydration requests travel from the CfSharp content provider to the instance Worker as bounded range reads.
- Local feed batches are validated, converted into upload operations, and acknowledged after durable queue acceptance.
- Remote batches are applied in order. Provider-originated changes receive echo suppression entries before the local feed can observe them.
- Cursors, recovery phases, and request fingerprints are persisted through the MP state boundary.
- Adapter Workers never receive the sync-root path, CfSharp database path, or MP catalog path.

## Recovery

On a normal restart, the Host restores the last phase and remote cursor before starting the feed. A failed or incomplete phase marks the checkpoint for a full rescan. The Host completes reconciliation first and acknowledges the CfSharp full-rescan request only after the new local view is durable.

The recovery harness exercises close, reopen, state-readability, and feed-readiness as one ordered scenario. Native Explorer checks remain a manual Windows step.

## Shutdown

1. The Host stops accepting new local operations.
2. In-flight Worker requests are cancelled and their sessions are closed.
3. Pending queue and recovery state are flushed.
4. The local change feed is disposed.
5. The CfSharp `CloudFileSystem` is disposed.
6. The owner lock is released.

Shutdown is idempotent at each boundary. A failure is recorded as a native error snapshot and leaves the recovery checkpoint available for the next startup.

## Account removal

Account removal unregisters the sync root first. MP-owned persisted state is cleared only after the native unregister operation succeeds. If unregister fails, state remains available for retry and diagnostics.

## Verification

The lifecycle code is covered by unit tests, the Windows Cloud Files integration harness, and the Explorer smoke checklist. Release validation also publishes the Host for `win-x64` and `win-arm64`.
