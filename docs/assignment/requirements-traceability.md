# Assignment Requirements Traceability Matrix

**Status: Final (Prompt 12)**  
Behavioral rows cite automated tests or reproducible demo steps. Document-only rows cite submission artifacts.

## Part A - Architecture and governance

| Requirement | Evidence | Verification |
|---|---|---|
| Component and data-flow diagram | `docs/packages/01_Architecture.pdf`; `docs/architecture/component-and-data-flow.mmd` | PDF visual QA (Prompt 11/12) |
| Shared versus FieldFlow-specific structure | `src/` project graph; Architecture PDF §5 | `ProjectDependencyTests`, `FieldFlowDtoBoundaryTests`, `CanonicalBoundaryTests` |
| Contractor mapping | `docs/architecture/schema-mapping.md`, `source-of-truth.md`; `FieldFlowContractorMapper` | `ContractorToVendorMapperTests`, `FieldFlowMapperValidationTests` |
| WorkOrder mapping | Same mapping docs; `FieldFlowWorkOrderMapper` | `WorkOrderToJobMapperTests`, `WorkOrderStatusMappingPolicyTests` |
| Field-level source of truth | `docs/architecture/source-of-truth.md` | `Proof360_originated_job_fields_are_not_overwritten`, outbound ownership test |
| Status-level source of truth | `JobStatusTransitionPolicy`; ADR-006 | `JobStatusTransitionPolicyTests`, `Newer_status_then_terminal_blocks_older_event_regression` |
| External/internal IDs and match key | `ProviderIdentityLink`; persistence | `UniquenessConstraintTests` |
| Deduplication | Inbox/outbox uniqueness + apply path | `Repeating_*`, `Valid_duplicate_*`, `Concurrent_duplicate_*` |
| Webhook verification | `FieldFlowWebhookVerifier`; `docs/architecture/webhooks.md` | `FieldFlowWebhookVerifierTests`, `WebhookSecurityTests` |
| Event ordering | Entity version + terminal policy | `Equal_version_*`, `Newer_status_then_terminal_blocks_older_event_regression` |
| Idempotency | Inbox event id, outbox key, payload hash | Concurrent + repeat inbound/outbound/webhook tests |
| Retry and replay | `FailureDispositionPolicy`; runbook | Resilience tests; `Replay_retains_event_identity_*` |
| Dead-letter handling | Inbox/outbox states + audit | `Exhausted_*`, `Permanent_unsupported_event_*`, `Unknown_event_type_*` |
| Circuit breaker/degraded mode | `docs/architecture/resilience.md`; ADR-007 | `HttpResiliencePipelineTests`, `ConnectorHealthTests`, `HostHealthEndpointTests` |
| Audit and correlation IDs | `StructuredAuditWriter`; middleware | `AuditReplayObservabilityTests`, `CorrelationIdRulesTests` |
| Monitoring, health, alerts, visible status | `docs/architecture/observability.md` | Health tests + connector health endpoint |
| Authentication and secrets | `.env.example`; config | Secret-marker tests; `Api_key_failure_returns_401`; Prompt 12 scan |
| Phase 1 and approval gates | `docs/assignment/phase-1-scope.md`; policies | `VendorApprovalPolicyTests`, outbound eligibility tests |
| Acceptance criteria (≥5 / target 10+) | Architecture PDF §19; `acceptance-criteria.md` | Tests + `03_README.md` demo |

## Part B - Working prototype

| Requirement | Implementation | Test / demo evidence |
|---|---|---|
| Connect to mock API | FieldFlow client + `PRG.FieldFlow.Mock` | Mock/integration smoke tests; README demo |
| Contractor to Vendor | ACL → `ApplyContractorSnapshot` | Mapper + repeating import tests |
| WorkOrder to Job | ACL → `ApplyWorkOrderSnapshot` | Mapper + repeating import tests |
| Preserve identity/lineage | Sidecar identity table | Uniqueness + link asserts |
| Prevent duplicate Jobs | Inbox/link uniqueness + TX | Concurrent duplicate tests |
| Idempotent status updates | Version + transition policy | Equal-version + terminal ordering tests |
| Safe retries and rate limits | Http.Resilience pipeline | 429 / 500 / timeout tests |
| Circuit protection | Configurable breaker | Open / short-circuit / half-open tests |
| Structured audit/failures | Append-only audit; typed failures | Audit/PII + ProblemDetails tests |
| Connector health | `/connectors/fieldflow/health` | Healthy/Degraded/Offline/NeedsAttention tests |
| Automated tests | Four test projects | Release suite (196) + TRX |
| Setup/run instructions | `03_README.md` (ZIP root) | Prompt 12 clean rehearsal |

## Part C - Leadership recommendation

| Requirement | Evidence |
|---|---|
| Decision, users, value, MVP, sequence, risks, ROI, alternatives, missing info | `docs/packages/04_Leadership_Recommendation.pdf` + ADR-008 |

## Part D - Scope and AI notes

| Requirement | Evidence |
|---|---|
| Phase 1 exclusions, second provider, AI tools, judgment, validation | `docs/packages/05_AI_and_Scope_Notes.md` |

## Submission instructions

| Instruction | Check |
|---|---|
| Exact ZIP name | `Jordaine_Gayle_PRG_Integration_Assignment.zip` |
| Required artifacts | `01`–`05` present at ZIP root; optional `06_Demo.mp4` omitted |
| Only local/mock data | Confirmed |
| No secrets or real data | Prompt 12 secret scan |
| Environment blockers | .NET 10 prerequisite in README |
| Packaging | Prompt 12 |

## Final evidence rule

No behavioral matrix row is “documented only.” Behavioral requirements point to automated tests or reproducible README demo steps.
