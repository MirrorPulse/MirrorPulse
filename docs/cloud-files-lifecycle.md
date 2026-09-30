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
before creating an instance. The instance form supplies non-secret Worker
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
  status and notifications. Upload-conflict resolution actions are still a
  product gate.
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

CfSharp `0.1.0-preview.2` provides the remote conflict and keep-local APIs
used here. The remaining product gates include Explorer Shell registration in
an interactive installed MSIX session, live WinUI configuration, and continuous
remote polling plus local move/delete dispatch and upload-conflict resolution.

Run `pwsh ./eng/verify-native.ps1` for the opt-in native checks. The ordinary CI
workflow runs the portable Release tests and both Host publish targets.
