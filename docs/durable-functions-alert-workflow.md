# Durable Functions Alert Workflow

## Regular Functions And Durable Functions

A regular Azure Function handles one trigger and runs until it succeeds or
fails.

For example, `ArchiveEventHubEvents` receives Event Hubs messages, archives
them, places processing references on Storage Queue, and finishes.

A Durable Function coordinates a multi-step workflow while Azure persists its
progress. The Phase 4 alert workflow is:

```text
Persist SQL alert
  -> Write Table Storage audit
  -> Send mock notification
```

`AlertOrchestration` controls the order, while `AlertActivities` performs the
external I/O.

## Why The Alert Workflow Is Durable

### Checkpointing

Durable Functions records each completed activity. If notification fails after
the SQL and audit activities succeed, the orchestration can retry from its
persisted history instead of restarting the entire workflow.

### Independent Retries

Each alert activity uses the orchestration retry policy:

- Three attempts.
- Five-second initial delay.
- Exponential backoff.
- Thirty-second maximum delay.
- Two-minute retry timeout.

### Host Restart And Scale-To-Zero

The Function host can restart, redeploy, or scale down between activities.
Durable Functions stores orchestration state in Azure Storage and resumes the
workflow when compute is available again.

### Observable Workflow State

Each alert has a Durable orchestration instance that can be inspected as
`Running`, `Failed`, or `Completed`. This is more useful operationally than one
regular Function invocation containing several unrelated external operations.

### Deterministic Identity And Deduplication

`ProcessQueuedEvent` starts the orchestration with `alert.AlertId` as its
instance ID. The alert ID is deterministic, and SQL persistence also treats a
duplicate alert ID as an idempotent result.

Table Storage audit writes use upsert behavior, so replaying or retrying that
activity does not create multiple audit entities.

### Separation Of Responsibilities

The orchestrator contains only workflow decisions:

```text
PersistAlert
WriteAlertAudit
SendMockAlertNotification
```

The activity functions contain the real external operations through
`IAlertRepository`, `IAuditRepository`, and `INotificationSender`.

Orchestrator code must remain deterministic. It should not access databases,
HTTP services, random values, or the current system clock directly.

## Why Not Use One Regular Function?

A regular Function could call all three operations directly:

```csharp
await PersistAlert();
await WriteAudit();
await SendNotification();
```

If the process stopped after writing SQL, the whole Function invocation could
run again. The application would then need to implement its own:

- Workflow checkpoints.
- Retry state.
- Deduplication.
- Recovery after host restarts.
- Per-alert workflow monitoring.

Durable Functions supplies those workflow guarantees. It would be unnecessary
for one isolated database write, but it is appropriate for this ordered,
retryable, multi-service alert workflow.

## Relevant Code

- `src/TflAnalytics.Processing.Functions/Functions/ProcessQueuedEvent.cs`
- `src/TflAnalytics.Processing.Functions/Functions/AlertOrchestration.cs`
- `src/TflAnalytics.Processing.Functions/Functions/AlertActivities.cs`
- `src/TflAnalytics.Processing.Functions/host.json`
