# Cursor Prompt 13 - Optional Five-Minute Demo Script and Rehearsal

---

Create and rehearse a concise demonstration of the final prototype. The recording must be no longer than five minutes and must show one complete failure/recovery path.

## Inspect first

Read the final README, endpoint contract, failure controls, health output, and demo scripts. Use only commands proven in the clean rehearsal. Do not change implementation unless a real demo-blocking defect is found.

## Deliverables

Create:

- `docs/assignment/demo-script.md`
- `scripts/run-demo.sh`
- `scripts/run-demo.ps1`

Scripts must use placeholder/local configuration, reset deterministic mock/state, and print concise labeled steps. They must not contain secrets or rely on an internet connection.

## Target narration and timing

### 0:00-0:30 - Purpose and boundaries

- State that this is a provider-neutral connector foundation with FieldFlow as the first adapter.
- Point out fixed Proof360 Vendor/Job schema and sidecar identity/inbox/outbox metadata.
- State at-least-once delivery with idempotent canonical effects.

### 0:30-1:30 - Healthy synchronization

- Show healthy connector status.
- Trigger contractor sync then work-order sync.
- Show Vendor/Job result and identity lineage using sanitized demo output.
- Repeat sync and show row counts unchanged.

### 1:30-2:20 - Duplicate and out-of-order webhook

- Send one signed status webhook.
- Repeat the exact event ID and show one applied effect plus duplicate audit.
- Send an older version and show Job status does not regress.

### 2:20-3:10 - Dependency handling

- Send/import a work order referencing an unknown contractor.
- Show WaitingForDependency and no invalid Job.
- Make contractor available/import it.
- Replay/process and show exactly one correctly assigned Job.

### 3:10-4:20 - Provider outage and recovery

- Configure deterministic provider failures.
- Trigger calls until the circuit opens.
- Show subsequent call short-circuited and connector health Offline/Degraded while liveness remains healthy.
- Restore FieldFlow, advance/wait only the documented short demo interval, perform half-open probe, and show recovery to Healthy.

### 4:20-4:50 - Evidence and conclusion

- Show concise audit/correlation and test summary.
- State how a second provider supplies an adapter while shared reliability/governance stays unchanged.
- Mention Phase 1 exclusions.

Leave ten seconds of timing margin. If the flow exceeds 4:50 in rehearsal, shorten narration rather than speeding through commands.

## Recording quality

- Use a clean terminal with readable font and no personal tabs/notifications.
- Hide environment variables and authorization/signature values.
- Use pre-positioned terminals or one reliable script to reduce mistakes.
- Avoid scrolling through source code; show behavior and evidence.
- Do not show real email, file paths containing personal/private data, or unrelated repositories.
- Record at a readable resolution and verify audio.
- Verify final MP4 duration, playback, and file size.

## Rehearsal requirements

- Run the complete demo twice from reset state.
- Confirm deterministic IDs/output where practical.
- Confirm no command requires manual secret entry on screen.
- Confirm failure/recovery completes within the time window.
- Confirm health and audit outputs are sanitized.

## Completion report

Report script paths, exact scenario order, two rehearsal durations, recording checklist, any skipped optional scenario, and whether the final `06_Demo.mp4` is present and under five minutes. Do not fabricate a recording if none was made.
