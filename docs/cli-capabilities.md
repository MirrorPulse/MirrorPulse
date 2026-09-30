# CLI capability matrix

This matrix is the completion gate for the no-UI control surface. Every capability listed here is available through `mp` and is backed by the current-user Host Control Client. The Control protocol is frozen at schema version **1** for the first product release.

| Capability | CLI command | Host Control operation | UI migration requirement |
| --- | --- | --- | --- |
| Host lifecycle | `mp host status\|start\|stop\|restart` | `host.status`, `host.start`, `host.stop`, `host.restart` | Use `MirrorPulseControlClient`; do not start Host from a page or view model. |
| Sync status and refresh | `mp status`, `mp sync status\|refresh` | `sync.status`, `sync.refresh` | Bind status cards and refresh actions to the same response models. |
| Adapter inventory | `mp adapter list` | `adapter.list` | Render registrations and versions from the topology response. |
| Adapter installation | `mp adapter install\|update\|remove` | `adapter.install`, `adapter.remove` | Reuse package validation, signature policy, and reference checks. |
| Instance lifecycle | `mp instance list\|create\|configure\|enable\|disable\|select-version` | `instance.*` | Keep instance IDs and version selection in Control models. |
| Conflict center | `mp conflict list\|show\|snooze\|resolve` | `conflict.*` | Use the same actions and preserve-path semantics. |
| Long-running work | `mp operation get\|watch\|cancel` | `operation.*` | Show progress from operation/event data rather than worker internals. |
| Settings and startup | `mp config`, `mp developer-mode`, `mp startup` | `settings.get`, `settings.update` | Keep settings persistence in Host; UI only edits typed settings. |
| Diagnostics | `mp diagnostics` | `diagnostics.collect` | Offer the same local archive options and privacy wording. |

## Frozen v1 rules

1. Requests and responses use `MirrorPulseControlSchema.CurrentVersion == 1`.
2. The named pipe is scoped to the current Windows user and carries framed JSON envelopes with a request ID.
3. A successful response has `ok: true` and optional `data`/`operationId`; a failure has `ok: false` and one structured `error`.
4. The CLI may start the current-user Host, but neither CLI nor UI addresses Adapter workers directly.
5. UI code must depend on `MirrorPulse.Control.Client` and `MirrorPulse.Control.Contracts`; it must not duplicate storage, worker, sync, conflict, or credential logic.
6. Future protocol changes require a new schema version and an explicit compatibility plan. Existing v1 clients remain supported for the first release line.

## UI migration checklist

- Replace page-local service calls with one shared `MirrorPulseControlClient` instance.
- Map command results and errors to the CLI JSON envelope models.
- Reuse CLI capability IDs, operation IDs, and conflict actions in navigation and notifications.
- Add UI tests against the same Control pipe fixture used by `eng/verify-cli-integration.ps1`.
- Keep UI optional: every release capability must remain usable with `mp` when the UI is not running.
