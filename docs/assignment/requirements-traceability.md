# Assignment Requirements Traceability Matrix

Filled for Prompt 10 against this repository. Behavioral rows cite automated tests (and source paths). Part C / packaging rows remain Prompt 11–12 deliverables.

## Part A - Architecture and governance

| Requirement | Planned evidence | Verification |
|---|---|---|
| Component and data-flow diagram | `docs/architecture/*`, ADR set; Architecture PDF (Prompt 11) | Visual PDF review (Prompt 11) |
| Shared versus FieldFlow-specific structure | `src/` project graph; `docs/architecture/source-of-truth.md` | `ProjectDependencyTests`, `FieldFlowDtoBoundaryTests`, `CanonicalBoundaryTests` |
| Contractor mapping | `docs/architecture/mapping-contractor.md`; `FieldFlowContractorMapper` | `ContractorToVendorMapperTests`, `FieldFlowMapperValidationTests` |
| WorkOrder mapping | `docs/architecture/mapping-work-order.md`; `FieldFlowWorkOrderMapper` | `WorkOrderToJobMapperTests`, `WorkOrderStatusMappingPolicyTests` |
| Field-level source of truth | `docs/architecture/source-of-truth.md` | `Proof360_originated_job_fields_are_not_overwritten`, `Provider_response_does_not_overwrite_proof360_owned_fields` |
| Status-level source of truth | `JobStatusTransitionPolicy`; ADR-006 | `JobStatusTransitionPolicyTests`, `Newer_status_then_terminal_blocks_older_event_regression` |
| External/internal IDs and match key | `ProviderIdentityLink`; persistence | `UniquenessConstraintTests`, inbound link-count asserts |
| Deduplication | Inbox/outbox uniqueness + apply path | `Repeating_*`, `Valid_duplicate_*`, `Concurrent_duplicate_*` |
| Webhook verification | `FieldFlowWebhookVerifier`; `docs/architecture/webhooks.md` | `FieldFlowWebhookVerifierTests`, `WebhookSecurityTests` |
| Event ordering | Entity version + terminal policy | `Equal_version_*`, `Newer_status_then_terminal_blocks_older_event_regression` |
| Idempotency | Inbox event id, outbox key, payload hash | Inbound/outbound/webhook concurrent + repeat tests |
| Retry and replay | `FailureDispositionPolicy`; runbook | Resilience tests; `Replay_retains_event_identity_*`; `docs/runbooks/replay-and-dead-letter.md` |
| Dead-letter handling | Inbox/outbox states + audit | `Exhausted_*`, `Permanent_unsupported_event_*`, `Unknown_event_type_*` |
| Circuit breaker/degraded mode | `docs/architecture/resilience.md`; ADR-007 | `HttpResiliencePipelineTests`, `ConnectorHealthTests`, `HostHealthEndpointTests` |
| Audit and correlation IDs | `StructuredAuditWriter`; correlation middleware | `AuditReplayObservabilityTests`, `CorrelationIdRulesTests`, webhook correlation test |
| Monitoring, health, alerts, visible status | `docs/architecture/observability.md`; health endpoints | `ConnectorHealthTests`, `HostHealthEndpointTests`, `Dead_letter_count_affects_connector_health_degraded` |
| Authentication and secrets | `.env.example`; config options | Secret-marker tests; mock `Api_key_failure_returns_401` |
| Phase 1 and approval gates | Scope docs; `VendorApprovalPolicy` | `VendorApprovalPolicyTests`, outbound eligibility tests |
| At least five acceptance criteria | Demo sync/dispatch/health flows in README | Runnable smoke tests + local rehearsal (Prompt 10) |

## Part B - Working prototype

| Requirement | Planned implementation | Required test evidence |
|---|---|---|
| Connect to mock API | `PRG.Proof360.Integrations.FieldFlow` + `PRG.FieldFlow.Mock` | `FieldFlowMockEndpointTests`, `RunnableHostSmokeTests`, `MockHostSmokeTests` |
| Contractor to Vendor | ACL mapper → `ApplyContractorSnapshot` | `Complete_contractor_*`, `Repeating_contractor_snapshot_*` |
| WorkOrder to Job | ACL mapper → `ApplyWorkOrderSnapshot` | `Complete_work_order_*`, `Repeating_work_order_snapshot_*` |
| Preserve identity/lineage | Sidecar identity table | Uniqueness + import link asserts |
| Prevent duplicate Jobs | Inbox/link uniqueness + TX | `Concurrent_duplicate_snapshots_*`, `Concurrent_duplicate_webhooks_*` |
| Idempotent status updates | Version + transition policy | Equal-version same/conflict; terminal + older |
| Safe retries and rate limits | Http.Resilience pipeline | `Transient_500_*`, `Rate_limit_honours_Retry_After_*`, `Timeout_retries_*` |
| Circuit protection | Configurable breaker | Open / short-circuit / half-open success+failure tests |
| Structured audit/failures | Append-only audit; typed failures | Audit PII/secret tests; `ProblemDetailsMapperTests` |
| Connector health | `/connectors/fieldflow/health` | Healthy/Degraded/Offline/NeedsAttention + freshness/unresolved inputs |
| Automated tests | Four focused test projects | Release TRX under `artifacts/test-results/` |
| Setup/run instructions | Root `README.md` | Clean-run rehearsal |

### Prompt 10 category → primary tests

| Category | Primary evidence |
|---|---|
| Canonical and architecture | `CanonicalFieldContractTests`, `CanonicalColumnTests`, `ProjectDependencyTests`, `FieldFlowDtoBoundaryTests` |
| Mapping and governance | Mapper/policy unit tests; inbound origin/approval tests |
| Idempotency and ordering | Inbound/webhook concurrent (≥10), equal-version, terminal ordering, `Failed_apply_does_not_mark_inbox_completed_*` |
| Dependencies, retry, replay | Wait/resolve/exhaust DLQ; resilience 429/500/timeout/400/401; cancellation; replay idempotency |
| Circuit, health, observability | Circuit suite; liveness vs provider; health snapshot inputs; audit/correlation; telemetry label sanitization |
| Outbound dispatch | Eligibility; concurrent outbox (10); stable idempotency key; atomic success; ambiguous reconcile; failed local recovery |

## Part C - Leadership recommendation

| Requirement | Planned content |
|---|---|
| Recommended integration and target user | Prompt 11 Leadership PDF (Accounting first; ADR-008) |
| Business/revenue value | Prompt 11 |
| Sequence and timeline assumptions | Prompt 11 |
| Dependencies/risks/assumptions | Prompt 11 + `docs/assignment/assumptions.md` |
| MVP and success measures | Prompt 11 |
| ROI | Prompt 11 |
| Why others are not first | Prompt 11 |
| Missing information | Prompt 11 |

## Part D - Scope and AI notes

| Requirement | Planned evidence |
|---|---|
| Intentional Phase 1 exclusions | `docs/assignment/*`; AI notes (Prompt 11) |
| Second provider | Capability ports in Core; adapter explanation in Architecture PDF |
| AI tools and assistance | Prompt 11 `05_AI_and_Scope_Notes.md` |
| Personal engineering judgment | ADRs 001–008; source-of-truth decisions |
| AI validation | Diff review, tests, this matrix, Release build, PDF visual check (11–12) |

## Submission instructions

| Instruction | Check |
|---|---|
| Exact ZIP name | Prompt 12 |
| Required five artifacts | Prompt 11–12 |
| Optional demo maximum five minutes | Prompt 13 optional |
| Only local/mock data | Confirmed in README |
| No secrets or real data | Prompt 12 secret scan |
| Environment blockers stated | README prerequisites (.NET 10) |
| Single attachment or download link | Prompt 12 |

## Final evidence rule

No matrix row may say only "documented" when the requirement is behavioral. Behavioral requirements above point to automated tests or an explicit later-prompt artifact.
