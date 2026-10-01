# Client ownership gate

Run `pwsh -File eng/verify-architecture.ps1` to inspect CLI, Control and App
project references and source boundaries. CI runs this gate before building.

The Host owns the Cloud Files session, official CfSharp store, product catalog,
credentials, synchronization and Worker lifecycles. Clients send typed requests
through MirrorPulse.Control.Client. No client may create an independent store
or use private CfSharp tables.

## Current allowances

- CLI directly references Control.
- Control still references Core for legacy pure version-one DTOs. This does not
  permit State, SQLite or Cloud Files access.
- App still references Core and CloudFiles.CfSharp. Its two read-only catalog
  pages and conditional packaged diagnostic probe are migration exceptions.

`eng/architecture-exceptions.json` records exact legacy source lines and their
reasons. A new persistence call or direct storage package/reference fails the
gate. Linked source outside the reviewed client project is rejected. Removing
legacy code requires removing its stale exception, not expanding the allowlist.

The gate checks references and source patterns; it enforces repository
architecture rather than a Windows security sandbox. It does not assert that
an assembly cannot be reached through reflection or transitive dependencies.

The App allowances must be retired when UI moves to Control.Client. Until then,
the gate reports their count and the [capability baseline](cli-capabilities.md)
does not claim that UI migration is complete.
