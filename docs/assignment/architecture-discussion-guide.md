# Architecture Discussion Guide

Prompts to answer without reading code. Keep answers short; point to ADRs when challenged.

## Boundaries and model

- Why a modular monolith instead of microservices for this prototype?
- What lives in Domain vs Application vs Core vs FieldFlow vs Infrastructure vs API?
- Why are `ProviderIdentityLink` / inbox / outbox not violations of “only three canonical entities”?
- Why must FieldFlow IDs never become `vendor_id` / `job_id`?
- Why are `ai_json` / `rationale` / `notes_scope` unsafe as dump fields?

## Identity and truth

- How do internal UUIDs and external `(provider_instance, type, id)` relate?
- What is the match key strategy — and why no fuzzy matching?
- Field-level source of truth for Contractor→Vendor and WorkOrder→Job?
- Origin rule: Proof360-originated vs FieldFlow-originated Jobs?
- Why provider reactivation cannot auto-approve a Vendor?

## Delivery and consistency

- Why say **at-least-once + idempotency** instead of exactly-once?
- How do polling and webhooks avoid two mapping/dedupe implementations?
- Inbox uniqueness vs identity-link uniqueness vs transactional upsert — why all three?
- Transaction boundaries inbound and outbound — why no DB transaction across HTTP?
- What happens on duplicate webhook? Concurrent duplicate delivery?

## Ordering and dependencies

- How do event versions prevent status regression?
- What if version is missing or untrusted?
- Unknown contractor path end-to-end?
- Dead-letter vs retry vs replay — who triggers what?

## Outbound and resilience

- Why outbox before calling FieldFlow?
- Why a stable Idempotency-Key on POST work-orders?
- Ambiguous POST (timeout after send) — reconcile before retry?
- Who owns HTTP retries vs inbox/outbox attempts?
- Circuit open / half-open / recovery and user-visible connector status?
- Why FieldFlow outage must not take down Proof360 liveness?

## Evolution and product

- How would a second provider be added with narrow capability ports?
- What moves from SQLite/in-process to PostgreSQL/broker later?
- What is intentionally out of Phase 1 and why?
- Why recommend Accounting first commercially (and why not Field service / Signal / Call intel / Funding first)?
- What ROI inputs are assumptions vs facts?
- What would you ask PRG before production go-live?

## Security and privacy

- Webhook HMAC steps and constant-time compare?
- What must never appear in logs?
- Where do API keys and secrets live in prototype vs production?
