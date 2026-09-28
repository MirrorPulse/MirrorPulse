# Cloud Files Lifecycle

MirrorPulse has one current-user Cloud Files sync root. CfSharp owns its native
namespace and coordination database. MirrorPulse owns Adapter installation,
instance routing, user policy, and a separate product catalog.

## Startup and shutdown today

1. The Host checks the Windows version, loads the display name from product
   configuration, and derives separate sync-root and data-root paths.
2. `MirrorPulseCloudHostSession` obtains the current-user owner lock and ensures
   the Shell and CfSharp registrations agree with its stable root identity.
3. The session starts one `CloudFileSystem` with the official
   `CfSharp.Storage.Sqlite` factory. Without configured Adapters, its demand
   provider exposes an empty root.
4. Cancellation disposes the CfSharp session and releases the owner lock.
   Registration and the SQLite database remain for the next run. Explicit
   account removal has a separate unregister path.

The Host does not yet launch installed Adapter Workers or connect its existing
Named Pipe transport to this session. Those startup and shutdown steps belong
to the forthcoming Worker integration, and their absence must not be inferred
from the integration-layer tests.

## Integrated data paths

- First-level Adapter labels are routed within the same root. One instance may
  own multiple labels, and multiple instances of one Adapter have distinct
  identities. Duplicate active labels are rejected before population.
- CfSharp requests directory pages and file ranges through the MirrorPulse
  demand provider. It retains native continuation and hydration semantics.
  MirrorPulse translates callbacks to Adapter paths and protects instance
  boundaries, including volume-rooted paths supplied by Windows.
- CfSharp's local change feed is the authoritative pending upload journal.
  MirrorPulse maps entries to Worker commands and schedules retries; successful
  uploads are acknowledged with the accepted remote revision.
- Adapter remote batches map once to CfSharp batches. CfSharp persists applied
  progress and conflicts; MirrorPulse advances its named checkpoint only after
  a safe result. Replaying the same batch covers a crash between those writes.
- CfSharp suppresses local echoes from remote changes in the sync root.
  A local-directory Adapter separately suppresses its own source-tree watcher
  echoes because that is a different file tree.

## Recovery evidence and remaining gates

An opt-in Windows test uses an external consumer process to enumerate a
partial directory and read an online-only placeholder, then reopens the same
CfSharp database and reads again. Another test exits a separate process with
an open SQLite transaction and verifies committed journal, partial batch,
conflict, echo, and checkpoint data survive while the unfinished write does
not. It also performs a SQLite integrity check.

The current preview fails to apply a newly created remote directory with echo
suppression because of a SQLite foreign-key error. A corrected CfSharp package
must pass successful partial application and replay before that path is called
complete. Durable conflict detail reads and a terminal keep-local decision also
need public CfSharp APIs. See the compatibility report for the exact limits.

Run `pwsh ./eng/verify-native.ps1` for the opt-in native checks. The ordinary CI
workflow runs the portable Release tests and both Host publish targets.
