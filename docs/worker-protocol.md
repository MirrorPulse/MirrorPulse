# MirrorPulse Worker Protocol

This document describes the version-one control contract between MirrorPulse and
one Adapter Worker process. It records the contract implemented by the Core
models; transport services and message serialization are introduced in later
commits.

## Process and identity boundary

MirrorPulse starts one Worker process for each Adapter Instance. A Worker is
identified by its `adapterId`, `installId`, `instanceId`, and
`workerSessionId`. A session ID changes every time the Worker process starts.
Root registrations carry an Adapter-provided uniqueness key so MirrorPulse can
reject a first-level directory collision without renaming the directory.

## Handshake

The expected sequence is:

1. MirrorPulse creates a current-user Named Pipe and starts the Worker.
2. The Worker sends `Hello` with its Adapter identity, package version,
   runtime identifier, supported protocol range, and manifest SHA-256.
3. MirrorPulse selects one common protocol version or rejects the session with
   a stable error code.
4. MirrorPulse sends `Ready` with the selection and the root registrations that
   the instance may expose.

`Hello` and `Ready` use the same instance and Worker session context. A failed
selection never becomes a healthy Worker session.

## Control frames

Named Pipe control frames use a four-byte unsigned little-endian payload length
followed by the payload bytes. The maximum control payload is 4 MiB. Control
payloads are UTF-8 JSON and contain a `ControlFrameEnvelope` with:

- protocol version;
- message type;
- request ID and response flag;
- instance ID and Worker session ID;
- message-specific JSON payload.

Requests and responses share a correlation request ID. Cancellation targets the
request ID of the operation being cancelled. Each Worker event has its own event
ID and monotonic per-session sequence.

## Lifecycle messages

- `Heartbeat` and `Health` provide liveness and a health status of healthy,
  degraded, or unhealthy.
- `Cancel` identifies a target request, reason, and optional force behavior.
- `Shutdown` carries a reason, grace period, and optional restart request.
- Retry directives carry an attempt number, delay, retry time, and optional
  reason. Backoff settings declare initial and maximum delays and a multiplier.

## Errors, diagnostics, and logs

`ErrorInfo` carries a stable code, category, retryability, authentication,
conflict, unsupported, native error, and diagnostic ID fields. Host-side codes
use the `mp.` namespace; Worker-originated codes use `worker.`.

Diagnostic events retain structured diagnostics and correlation metadata.
Structured log fields classify common password, token, secret, API key, private
key, and credential-value names as sensitive and replace their values with
`[REDACTED]` before the log entry is created. Credential references identify
current-user secure-store entries and never contain secret material.

## Compatibility rules

Protocol ranges are negotiated before a Worker is considered ready. Unknown
message types and unsupported protocol versions must produce structured errors;
they must not silently downgrade security or execute package code. New optional
fields may be added with a compatible schema revision, while changes to frame
length, identity, or correlation semantics require a protocol revision.
