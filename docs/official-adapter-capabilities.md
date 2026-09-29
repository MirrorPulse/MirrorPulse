# Official Adapter capability evidence

This matrix records behavior observed in the current source tree and signed
official releases. A capability is advertised only after a protocol
or filesystem boundary test proves it. The Host must not infer a capability
from the storage protocol's theoretical feature set.

| Adapter | Read | Write | Move | Delete | Offline upload | Conflict detection |
| --- | --- | --- | --- | --- | --- | --- |
| Local directory | Signed Worker + CfSharp demand | Signed Worker + Host upload | Not exposed by Worker | Not exposed by Worker | Journal retry path only | Revision check |
| WebDAV | Signed Worker + CfSharp demand | Signed Worker + Host upload | Not exposed by Worker | Not exposed by Worker | Not verified end to end | Conditional ETag check |
| SMB | Signed Worker; no live share fixture | Signed Worker; no live share fixture | Not exposed by Worker | Not exposed by Worker | Not verified end to end | Revision check |
| FTP / FTPS | Loopback Worker process | Loopback Worker process | Not exposed by Worker | Not exposed by Worker | Transfer retry only | Optimistic revision check |
| SFTP | Loopback Worker process | Loopback Worker process | Not exposed by Worker | Not exposed by Worker | Transfer retry only | Optimistic revision check |

The WebDAV loopback fixture exercises the installed signed Worker, Host pipe,
CfSharp-compatible demand reads, and stale ETag upload rejection without a
remote overwrite. It does not establish compatibility with every WebDAV server.
“Journal retry path” and “transfer retry only” do not establish end-to-end offline queuing through
Explorer and CfSharp. The SMB release contains a real Worker, but a live UNC
share test is still needed before its transfer capability is fully verified.

The current FTP and SFTP Workers expose `Stat`, `ReadRange`, and `Upload`.
Their tests launch separate EXEs, pass credentials through the current-user
pipe, perform real protocol transfers, and check stale revisions. FTP tests
cover plain FTP and both FTPS TLS modes. SFTP tests cover host-key approval and
pinning, server disconnect, Worker restart, and retry. Both revision checks
combine length with remote modification time; neither protocol path has an
atomic compare-and-swap, so a concurrent writer may still win between the
last check and rename. These Workers must not advertise strong conditional
write or conflict-free offline synchronization.

Evidence:

- `MirrorPulseLocalDirectoryReaderTests`,
  `MirrorPulseLocalDirectoryWriterTests`, and
  `MirrorPulseLocalDirectoryMutatorTests` use temporary local files.
- `MirrorPulseWebDavRangeReadTests`,
  `MirrorPulseWebDavUploadTests`, and
  `MirrorPulseWebDavEtagGuardTests` use HTTP handlers.
- `MirrorPulseSmbDirectoryPollerTests` uses an injected entry source; no
  real SMB capability is claimed.
- `FtpWorkerProcessTests` and `SftpWorkerTransferTests` run real protocol
  fixtures through independent Workers.
- `FtpSignedPackageProcessTests` and `SftpSignedPackageProcessTests` verify
  signed packages and start their installed Workers.
- `OfficialAdapterAggregateProcessTests` installs all five signed releases;
  the Local case starts two independent installed Workers and routes
  CfSharp-compatible demand enumeration and range reads through their separate roots.
- `SignedLocalVersionSwitchProcessTests` installs signed Local v0.1.3 alongside
  the latest release, observes the selected Worker executable in each process
  session, verifies reads, and confirms that a disabled instance starts no Worker
  until it is reenabled after a catalog restart.
- `SignedWebDavWorkerProcessTests` starts the signed WebDAV release against a
  loopback HTTP fixture and verifies directory ETag preservation, demand reads,
  stale upload rejection, and a successful conditional upload.

The matrix must be revised when Workers implement move/delete or full offline
and conflict handling is proven. The package and UI may expose only the
verified subset for a given installed version.
