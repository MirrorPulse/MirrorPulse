# Cloud Files Lifecycle

MirrorPulse has one current-user Cloud Files sync root. CfSharp owns its native
namespace and coordination database. MirrorPulse owns Adapter installation,
instance routing, user policy, and a separate product catalog.

## Startup and shutdown today

1. The Host checks the Windows version, loads product configuration and the
   installed Adapter topology, then derives separate sync-root and data-root paths.
2. `MirrorPulseCloudHostSession` obtains the current-user owner lock and ensures
   the Shell and CfSharp registrations agree with its stable root identity.
3. The session starts one `CloudFileSystem` with the official
   `CfSharp.Storage.Sqlite` factory. Its demand provider routes first-level
   directories to installed, enabled Adapter instances. Without configured
   Adapters, it exposes an empty root.
4. The Host starts one isolated Worker process and current-user Named Pipe for
   each enabled instance. Disabled instances retain visible offline directories.
5. Cancellation stops the Workers, disposes the CfSharp session, and releases
   the owner lock.
   Registration and the SQLite database remain for the next run. Explicit
   account removal has a separate unregister path.

The WinUI install flow verifies and registers a signed `.mpadapter` package
before creating an instance. Current packages carry their signed inventory at
`META-INF/mirrorpulse/signature.json`, so a local package can be installed
without a neighboring signature file. Older releases with a detached
`.signature.json` remain supported. The instance form supplies non-secret Worker
settings and first-level folder names through the current-user Host Pipe;
optional secrets are stored in Windows Credential Manager and only their
references enter the product catalog. New instances and version or startup
selection changes take effect after restarting MirrorPulse. Multiple instances
of the same Adapter require distinct first-level folder names.

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
  uploads are acknowledged with the accepted remote revision. Before an upload,
  MirrorPulse reads CfSharp's last mutually acknowledged item revision and
  compares it with a fresh Worker Stat. The acknowledged revision, never the
  fresh Stat value, is passed to the Worker's conditional upload. A mismatch
  persists an upload conflict in MP's separate catalog and stops automatic
  dispatch of that journal operation across restarts. The local content and
  remote file remain in place, and the Host exposes the pending conflict in
  status and notifications. Directory metadata notifications have no Worker
  transfer and are acknowledged separately so they do not leave a permanent
  pending-upload count. Conflict actions are available through the versioned
  Control client.
- Adapter remote batches map once to CfSharp batches. CfSharp persists applied
  progress and conflicts; MirrorPulse advances its named checkpoint only after
  a safe result. Replaying the same batch covers a crash between those writes.
- Active polling saves an immutable pending intent in the product catalog before
  applying it. A retry leaves the previous snapshot intact. On restart, the Host
  recreates and verifies the same batch before reading newer remote changes.
  After CfSharp reports the final safe cursor, the Host commits the candidate
  snapshot and clears the intent. A crash between these writes replays the batch;
  CfSharp remains the authority for already-applied entries and checkpoints.
- Background polling, manual refresh, and streamed remote applies share an
  instance scheduler. Different instances progress independently. A streamed
  batch cannot overtake a pending poll. Worker ingress uses a bounded ordered
  inbox so waiting for the scheduler never blocks the Pipe response reader.
- A failed journal command does not stop dispatch of later valid commands.
  Unacknowledged operations remain in the official feed. Acknowledgement failures
  have a distinct error code; source or catalog failures remain visible in the
  pump's in-memory health even when persistence is unavailable. Host status is
  degraded while that health reports a fault.
- Routing produces a decision for each journal observation. Root reconciliation,
  unknown paths, cross-root moves, and unsupported mutations are retained as
  blocked operations in the product catalog and exposed by sync status. They are
  never acknowledged as successful Worker mutations. Valid commands in the same
  feed batch continue. The later bounded journal paging work must also address
  batches filled entirely with unresolved blocked operations.
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

CfSharp `0.1.0-preview.2` provides the remote conflict and keep-local APIs
used here. The ARM64 CLI integration gate installs a signed Local Adapter,
reads a Cloud Files range, queues and drains an offline upload, applies a
remote batch, then verifies conflict and cursor state across Host restarts.
Interactive installed-MSIX Explorer validation and the planned WinUI migration
remain separate product gates.

Run `pwsh ./eng/verify-native.ps1` for the opt-in native checks. The ordinary CI
workflow runs x64 Release tests, x64/ARM64 CLI and Host publishes, and the
signed Local Adapter integration gate on ARM64. A manual CI dispatch also
checks native Cloud Files and installed MSIX identities.
