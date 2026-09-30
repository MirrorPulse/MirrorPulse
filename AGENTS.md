# MirrorPulse repository guidance

MirrorPulse is a Windows Cloud Files product for personal storage. It exposes one
current-user sync root and keeps configuration, catalog, caches, and diagnostics
in a separate private data root. The repository uses English for code, public
documentation, commit messages, and GitHub metadata.

## Ownership boundaries

- The Host owns the Cloud Files session, the official CfSharp SQLite store, the
  MirrorPulse product catalog, credentials, and Adapter Worker lifecycles.
- CLI and WinUI clients use the versioned `MirrorPulse.Control.Client` contract.
  Do not open the Host databases from a client or duplicate sync logic in UI.
- Keep CfSharp behind `MirrorPulse.CloudFiles.CfSharp`. Use its public, mature
  APIs and SQLite provider; retain the anti-corruption layer for product policy.
- Each enabled Adapter instance has its own Worker process and current-user
  Named Pipe. Installed `.mpadapter` packages contain the Worker executable and
  dependencies; the Host owns persistent settings and credential references.
- Multiple instances and multiple first-level roots per instance are supported.
  Preserve stable IDs, isolated state, and explicit disabled-root behavior.

## Changes and verification

- Make each commit independently reviewable and state the behavior it changes.
- Prefer focused tests that exercise a real boundary. Use a clean Windows test
  user for Cloud Files registration or installed MSIX checks.
- Run the relevant locked restore, Release build, tests, and formatting gate.
  The CI workflow covers x64 and ARM64 CLI/Host publishing and a signed Local
  Adapter end-to-end path on ARM64.
- Treat CfSharp preview behavior as version-specific. Report reproducible
  library defects with the package version and native error before adding a
  long-term workaround.
- Do not commit private keys, passwords, tokens, user files, or diagnostic data.
  Credential values may be accepted by CLI arguments but must not be echoed or
  recorded in ordinary logs.
