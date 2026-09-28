# Official Adapter capability evidence

This matrix records behavior observed in the current source tree. It is an
input to package manifests and the UI; it does not imply that an official
Adapter has been released. A capability is advertised only after a protocol
or filesystem boundary test proves it. The Host must not infer a capability
from the storage protocol's theoretical feature set.

| Adapter | Read | Write | Move | Delete | Offline upload | Conflict detection |
| --- | --- | --- | --- | --- | --- | --- |
| Local directory | Verified in-process | Verified in-process | Verified in-process | Verified in-process | Host pipeline only | Host pipeline only |
| WebDAV | HTTP handler test | HTTP handler test | PUT/MOVE handler test | Cleanup handler test only | Not verified | ETag handler test |
| SMB | Not verified on a share | Not verified on a share | Not verified on a share | Not verified on a share | Not verified | Not verified |
| FTP / FTPS | Loopback Worker process | Loopback Worker process | Not exposed by Worker | Not exposed by Worker | Transfer retry only | Optimistic revision check |
| SFTP | Loopback Worker process | Loopback Worker process | Not exposed by Worker | Not exposed by Worker | Transfer retry only | Optimistic revision check |

“In-process” means real local filesystem operations but no packaged Worker
process. “HTTP handler test” exercises HTTP method, header, and response
handling with a test handler; it is not a packaged WebDAV server test.
“Host pipeline only” and “transfer retry only” do not establish end-to-end
offline queuing through Explorer and CfSharp. The SMB row is deliberately
unadvertised because current tests use an injected entry source and do not
mount a real UNC share.

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

The matrix must be revised when a Worker implements move/delete, when
packaged Local/WebDAV/SMB Workers exist, or when end-to-end offline and
conflict handling is proven. The package and UI may expose only the verified
subset for a given installed version.
