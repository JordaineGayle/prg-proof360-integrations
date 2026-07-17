# Observable acceptance criteria (Phase 1)

| # | Criterion | Evidence |
|---|---|---|
| 1 | Canonical Vendor/Job/Transcript field contracts match assignment exactly | `CanonicalFieldContractTests`, `CanonicalColumnTests` |
| 2 | Domain/Application have no FieldFlow DTO dependency | `FieldFlowDtoBoundaryTests`, `ProjectDependencyTests` |
| 3 | Contractor import creates one Vendor + identity link under repeat poll | `Repeating_contractor_snapshot_yields_one_vendor_and_link` |
| 4 | Work-order import creates one Job; unknown contractor waits without partial Job | `Unknown_contractor_waits_*`, `Exhausted_unknown_contractor_*` |
| 5 | Valid webhook accepted only after durable inbox insert; invalid HMAC mutates nothing | `WebhookSecurityTests` |
| 6 | ≥10 concurrent duplicate webhooks → one inbox + one Job | `Concurrent_duplicate_webhooks_create_one_inbox_and_one_job` |
| 7 | Newer status applies; older after terminal cannot regress | `Newer_status_then_terminal_blocks_older_event_regression` |
| 8 | Equal-version same payload is no-mutation; different payload is conflict | `Equal_version_*` webhook tests |
| 9 | Qualified Job dispatch creates one outbox; ambiguous POST reconciles without duplicate | `OutboundDispatchTests` |
| 10 | 429 honours Retry-After; circuit opens/short-circuits/recovers; liveness ignores FieldFlow | `HttpResiliencePipelineTests`, `HostHealthEndpointTests` |
| 11 | Dead-letter replay is idempotent; audit omits secrets/PII markers | `AuditReplayObservabilityTests` |
| 12 | Local demo: mock + API sync + connector health without credentials | README rehearsal / Prompt 10 smoke |
