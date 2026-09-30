# MirrorPulse CLI

`mp` is the no-UI control surface for MirrorPulse. It sends versioned requests to the current-user Host over a per-user named pipe. If the Host is not running, the CLI starts the installed Host automatically. `--no-start` disables that behavior for callers that require an already-running service.

`mp host stop` and `mp host restart` return after the previous Host process has exited. A later ordinary command can then start the Host with the updated instance topology.

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
    --json       Emit the CLI schema-1 JSON envelope.
    --no-start    Require an already-running Host.
    --developer-mode
                 Allow the configured development Host executable.
    --timeout SEC
                 Set the Control request timeout.
```

The default output is concise human-readable text. Automation should use `--json`. Successful data commands emit `{"schemaVersion":"1","kind":"result","data":...}` to standard output; failed commands emit `{"schemaVersion":"1","kind":"error","exitCode":N,"code":"...","message":"..."}` to standard error. Help and version use the same schema with `kind` values `help` and `version`. Error details never include credentials or access tokens.

## Command reference

| Command | Purpose | Common arguments |
| --- | --- | --- |
| `mp status` | Read sync status, pending uploads, conflicts, instances, and transfer state. | `--json` |
| `mp host status\|start\|stop\|restart` | Inspect or control the current-user Host. | `mp --json host status` |
| `mp sync status\|refresh` | Read status or request a synchronization pass. | `mp --timeout 120 sync refresh` |
| `mp adapter list` | List installed Adapter packages, versions, and instances. | `--json` |
| `mp adapter install <package>` | Install a signed `.mpadapter` package. | `mp --developer-mode adapter install <package>` selects a development Host; the Host setting governs unsigned packages |
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

Global options must precede the command. Scalar command options accept `--name value` or `--name=value`; repeatable `--config`, `--root`, and `--enable-installation` options use separate values. Adapter configuration keys are opaque to MirrorPulse and are passed to the selected Adapter. Secrets are accepted as command arguments for compatibility with scripts; callers should prefer a protected process invocation and avoid shell history and log capture.

## Automation examples

```powershell
# Read a stable machine-readable status.
$status = mp --json status | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $status.kind -ne 'result') { throw 'MirrorPulse status failed.' }

# Request a refresh and inspect the resulting status.
mp --json sync refresh | ConvertFrom-Json

# Install and configure an Adapter instance.
$install = mp --json adapter install .\MirrorPulse.Adapter.Local.mpadapter | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Adapter installation failed.' }
mp instance create --install-id $install.data.installedAdapterId --name Documents `
  --config 'sourceDirectory=C:\Users\me\Documents'
```

The repository contains no-UI integration and regression fixtures in `eng/verify-cli-integration.ps1` and `eng/verify-cli-regression.ps1`. They are intended for a clean test user because Cloud Files registers one sync root per user.

## Exit codes and protocol

Successful commands return `0`. The process exit code table is:

| Code | Meaning |
| ---: | --- |
| 1 | Usage |
| 2 | Validation |
| 3 | Unavailable |
| 4 | Timeout |
| 5 | Authentication |
| 6 | Authorization |
| 7 | Conflict |
| 8 | Unsupported |
| 9 | Storage |
| 10 | Cancelled |
| 70 | Internal failure |

These values are defined by `MirrorPulse.Control.Contracts.MirrorPulseControlExitCodes`. CLI JSON schema 1 is distinct from Control protocol version 1, although both currently use version 1. The named pipe is restricted to the current user; Adapter workers remain isolated processes and are never addressed directly by the CLI.

The CLI is the supported automation surface. Future UI work must call the same Control client and preserve the schema-1 request and response contracts.
