# Test boundaries

Prepare the isolated SFTP fixture with `pwsh ./eng/setup-test-environment.ps1`
before running the managed solution tests. Environment-dependent tests report
inconclusive results when prerequisites are absent. `eng/verify-test-results.ps1`
checks executed counts and requires every named test in a dedicated suite.
Ordinary CfSharp tests are not all native Cloud Files tests.

## Durability faults

`DurabilityFaultFixtureTests` invokes a child process with a unique temporary
root. It injects either an exception or abrupt `Environment.Exit`, immediately
before or after each of these real persistence boundaries:

| Boundary | Actual API | Reopened observation |
| --- | --- | --- |
| Remote write | Atomic Local-directory writer against a fixture source | Old/new file bytes, no leftover temporary file |
| Journal acknowledgement | CfSharp public operation repository transaction | Pending operation present before commit, absent after commit |
| Poll snapshot | Product snapshot store | Old/new revision, complete JSON file |
| Product catalog | Product catalog command write | Old/new durable command state |
| Pending remote batch | Host catalog opaque replay intent and candidate snapshot | No record before save, identical batch/snapshot after save |

All 20 combinations execute in the managed suite. The probe requires a fixture
marker. It does not register a sync root, open user files, or install packages.
The original crash-probe mode still exercises an uncommitted CfSharp SQLite WAL
transaction in the native suite.

These fixtures establish deterministic failure boundaries. The journal case
uses the official store transaction rather than native change-feed delivery;
the remote case uses a local fixture source rather than a network Worker. They
do not establish production pump recovery or cross-system exactly-once delivery.
Those behaviors need the corresponding Host/Worker integration tests.

```powershell
dotnet test tests/MirrorPulse.CloudFiles.CfSharp.Tests --configuration Release --filter TestCategory=DurabilityFaultFixture
```

Native Cloud Files and installed MSIX checks must run under a clean Windows
test user or a disposable runner. Do not run installation/uninstallation probes
against a user's configured application or storage sources.

## CI evidence

Each CI job uploads `evidence-<job>` with schema 1 JSON containing the checked-out
commit, RID, executed/skipped counts by suite and category, and verified artifact
SHA-256 hashes. The collector re-hashes actual publish/package files and rejects
traversal paths, duplicate suites, mismatched counts, or missing required gates.
It exports only named checks and relative artifact paths, without TRX output,
stack traces, host user paths, credentials, or temporary certificate material.

A manual workflow run additionally requires eight actual native Cloud Files
integrations, one official SQLite crash test, and installed ARM64 MSIX checks
on the Windows 11 desktop runner. The x64 Server runner verifies that published
CLI and Host processes reject the unsupported SKU without creating state.
The legacy native filter also selects five
managed helper tests; they remain a separate category. MSIX installation,
CLI alias, Host auto-start, associations, and uninstall checks are separate from
Shell registration. `shellRegistration=false` explicitly means it was not run.
Installed evidence includes the OS build and product type; a Server installation
cannot satisfy the [supported desktop matrix](windows-support.md).
The ARM64 report requires signed Local CLI regression evidence as well as the
two signed Worker tests; the official aggregate requires all seven named tests.

Download the three `evidence-*` artifacts from one workflow run, then verify:

```powershell
pwsh ./eng/verify-ci-evidence.ps1 -EvidenceDirectory artifacts/downloaded-evidence -ExpectedSourceSha <commit-sha> -RequireNative -RequireInstalled
```

Omit the last two switches for a push/PR run, which does not select those gates.
The three reports must name the same expected commit and their job-specific RID.
