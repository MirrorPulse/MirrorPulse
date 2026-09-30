# MirrorPulse CLI

`mp` is the no-UI control surface for MirrorPulse. It sends versioned requests to the current-user Host over a per-user named pipe. If the Host is not running, the CLI starts the installed Host automatically. `--no-start` disables that behavior for callers that require an already-running service.

## Installation and builds

The MSIX package exposes an `mp.exe` app execution alias and carries the architecture-matched Host under `host\MirrorPulse.Host.exe`. Development builds can run the published executable directly:

```powershell
dotnet run --project src/MirrorPulse.Cli -- --version
pwsh -File eng/verify-cli-host-publish.ps1 -Runtime win-x64
pwsh -File eng/verify-cli-host-publish.ps1 -Runtime win-arm64
```

Use the ARM64 package on ARM64 Windows. The CLI and Host must come from the same build so their Control protocol and runtime dependencies match.

## Global options

```text
-h, --help       Show help.
    --version    Print the CLI version.
    --json       Emit the schema-1 JSON envelope.
    --no-start    Require an already-running Host.
    --developer-mode
                 Allow the configured development Host executable.
    --timeout SEC
                 Set the Control request timeout.
```

The default output is concise human-readable text. Automation should use `--json` and inspect the `schemaVersion`, `ok`, `data`, and `error` fields. Error details never include credentials or access tokens.

## Command reference

| Command | Purpose | Common arguments |
| --- | --- | --- |
| `mp status` | Read sync status, pending uploads, conflicts, instances, and transfer state. | `--json` |
| `mp host status\|start\|stop\|restart` | Inspect or control the current-user Host. | `--no-start` |
| `mp sync status\|refresh` | Read status or request a synchronization pass. | `--timeout 120` |
| `mp adapter list` | List installed Adapter packages, versions, and instances. | `--json` |
| `mp adapter install <package>` | Install a signed `.mpadapter` package. | `--developer-mode` only for a deliberately unsigned package |
| `mp adapter update <package>` | Install a newer package while retaining eligible versions. | package path |
| `mp adapter remove --adapter-id ID [--install-id ID] [--purge]` | Remove an installation after Host reference checks. `uninstall` is an alias. | `--purge` removes retained package data when safe |
| `mp instance list` | List Adapter instances and mapped roots. | `--json` |
| `mp instance create --install-id ID --name NAME [--config k=v] [--root k=v] [--secret VALUE] [--disabled]` | Create an independently identifiable instance. | repeat `--config`/`--root` for multiple values |
| `mp instance configure --instance-id ID --name NAME [--config k=v] [--root k=v]` | Update instance configuration and roots. | adapter-defined keys |
| `mp instance enable\|disable --instance-id ID` | Change whether an instance participates in synchronization. | `--json` |
| `mp instance select-version --instance-id ID --install-id ID` | Select the installed Adapter version for an instance. | `--json` |
| `mp conflict list\|show\|snooze\|resolve` | Inspect, defer, or resolve conflicts. | `--conflict-id ID`, `--action KeepLocal\|KeepRemote\|KeepBoth\|Retry\|DeleteLocal\|DeleteRemote`, `--preserved-path PATH` |
| `mp operation get\|watch\|cancel --operation-id ID` | Inspect or cancel a long-running operation. | `--timeout SEC` |
| `mp config` | Read settings, or update them with `--locale`, `--developer-mode`, `--start-with-windows`, `--enable-installation ID`, and `--sync-root-display-name NAME`. | `--json` |
| `mp developer-mode [--developer-mode true\|false]` | Read or change the global unsigned-Adapter/development Host switch. | `--developer-mode false` |
| `mp startup [--start-with-windows true\|false]` | Read or change optional startup. | `--start-with-windows true` |
| `mp diagnostics [--include-logs] [--output PATH]` | Create a local diagnostic archive. | logs stay local unless the user shares them |

Options can be written as `--name value` or `--name=value`. Adapter configuration keys are opaque to MirrorPulse and are passed to the selected Adapter. Secrets are accepted as command arguments for compatibility with scripts; callers should prefer a protected process invocation and avoid shell history and log capture.

## Automation examples

```powershell
# Read a stable machine-readable status.
$status = mp --json status | ConvertFrom-Json
if (-not $status.ok) { throw $status.error.message }

# Request a refresh and inspect the resulting status.
mp --json sync refresh | ConvertFrom-Json

# Install and configure an Adapter instance.
mp adapter install .\MirrorPulse.Adapter.Local.mpadapter
mp instance create --install-id local-1 --name Documents `
  --config sourceDirectory=C:\Users\me\Documents `
  --root folder=Documents
```

The repository contains no-UI integration and regression fixtures in `eng/verify-cli-integration.ps1` and `eng/verify-cli-regression.ps1`. They are intended for a clean test user because Cloud Files registers one sync root per user.

## Exit codes and protocol

Successful commands return `0`. Validation failures, unavailable Host/Adapter operations, cancellation, and unsupported commands have stable non-zero values defined by `MirrorPulse.Control.Contracts.MirrorPulseControlExitCodes`. JSON output uses Control schema version 1. The named pipe is restricted to the current user; Adapter workers remain isolated processes and are never addressed directly by the CLI.

The CLI is the supported automation surface. Future UI work must call the same Control client and preserve the schema-1 request and response contracts.
