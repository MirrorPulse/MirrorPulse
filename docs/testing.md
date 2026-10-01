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

All 16 combinations execute in the managed suite. The probe requires a fixture
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
