# Leadership Recommendation — Accounting First

**Author:** Jordaine Gayle  
**Deliverable:** `04_Leadership_Recommendation.pdf`  
**Status:** Recommend Accounting as the first commercial integration investment, **subject to discovery validation**.  
**Technical proving ground:** FieldFlow field-service connector prototype (reliability patterns reusable).

---

## 1. Decision and target users

**Decision:** Prioritize **Accounting** integration next (invoice/bill sync and reconciliation), not payments/funding and not insight-only products as the first commercial bet.

**Primary users**

| Audience | Why they care |
|---|---|
| Finance operations / AP–AR | Fewer manual invoice/bill touches; cleaner exception queues |
| Implementation & support | Repeatable sync + reconciliation playbooks |
| Customers needing accounting sync | Faster close visibility without reinventing field ops |

Interview signal retained: **transaction creation must precede payments**, and invoice automation requires **human approval boundaries**.

---

## 2. Business value

| Value lever | Effect |
|---|---|
| Automated transaction creation | Invoices/bills enter the system of record without re-keying |
| Lower manual touch / error cost | Fewer exceptions from transcription and status drift |
| Faster reconciliation / close / cash visibility | Ops sees sync state and blockers earlier |
| Retention & expansion | Sticky finance workflow once trust is earned |

Field service remains strategically useful as **ops glue** and as the engineering pattern library (inbox/outbox/idempotency/circuit/health). It is not the strongest first **finance ROI** narrative for this investment decision.

---

## 3. MVP (and hard non-goals)

**MVP includes**

- Invoice/bill **transaction creation** into accounting  
- Sync **status** visibility (healthy / degraded / exception)  
- Reconciliation + **exception handling** queue  
- Human approval for high-risk actions  

**MVP excludes**

- Automatic **payment** movement / funding rails  
- Closed-period writebacks without finance controls  
- Replacing the ERP as system of record  

---

## 4. Sequence and timeline assumptions

Assumptions — not a committed program plan.

| Phase | Focus | Exit signal (assumption) |
|---|---|---|
| 1. Discovery / data contract | Chart of accounts, tax, entities, period rules | Signed field map + ownership |
| 2. Core transaction sync | Create/update invoice/bill + status | Pilot tenants create without dual-entry |
| 3. Exceptions / reconciliation | Mismatch queue, replay, audit | Exception rate under target |
| 4. Controlled pilot | One provider + limited customers | Finance signs control review |
| 5. Scale decision | Expand providers/tenants or pause | ROI inputs validated |

---

## 5. Dependencies, risks, and assumptions

| Item | Note |
|---|---|
| Closed accounting periods | Writes must respect period locks |
| Supplemental transactions | Credit memos / adjustments need explicit rules |
| Provider API maturity | Auth, idempotency, webhooks, SLAs vary |
| Customer concentration | ROI sensitive to a few large accounts |
| Security / compliance | Finance data heightens review bar |
| Delivery capacity | Connector patterns exist; accounting domain rules still TBD |

**Risk:** shipping payments/funding first creates compliance and irreversible-money risk before accounting truth is solid.

---

## 6. Success measures (targets / assumptions)

All thresholds are **targets pending PRG baselines** — not measured facts.

| Measure | Target assumption |
|---|---|
| Manual touch minutes per invoice/bill | ↓ ≥ 50% vs baseline |
| Exception rate on synced transactions | &lt; 5% after pilot stabilize |
| Time-to-visible sync failure | &lt; 15 minutes (health + alerts) |
| Dual-entry incidents in pilot | 0 material duplicates |
| Finance control review | Pass before scale |

---

## 7. ROI methodology (transparent; no invented PRG figures)

Do **not** invent PRG volumes, labour rates, or revenue. Use discovery inputs in:

```text
Annual benefit =
  (volume × minutes saved × loaded labour rate)
  + error costs avoided
  + cash-flow / retention benefit
  − build and operating cost
```

| Input | Source required |
|---|---|
| Volume | Monthly invoice/bill count in scope |
| Minutes saved | Time study / SME estimate |
| Loaded labour rate | Finance ops cost model |
| Error costs avoided | Rework, credit notes, write-offs |
| Cash-flow / retention | Finance + CS judgment (label as assumption) |
| Build + operating cost | Eng + vendor + support run-rate |

Commit only after the formula is populated with PRG data — not narrative alone.

---

## 8. Why not first (without dismissing value)

| Option | Keep the value | Why not first |
|---|---|---|
| **Field service** | Proven here as reliability spine; ops efficiency | Narrower finance ROI story vs accounting sync |
| **Signal ingestion** | Rich operational signal | Insight without accounting backbone under-delivers cash/close impact |
| **Call intelligence** | Transcript/summary upside | Depends on AI quality + downstream actionability; weaker immediate AP/AR ROI |
| **Funding / payments** | High strategic upside later | Highest risk/compliance; needs accounting truth + approval controls first |

---

## 9. Information required before final commitment

1. Invoice/bill **volumes** and seasonality  
2. Current **touch time** and labour rates  
3. Exception / error baselines and cost  
4. Provider and customer **concentration**  
5. Willingness to pay / packaging  
6. Accounting provider **API, security, SLA** evidence  
7. Delivery capacity and finance ownership for period controls  

---

## 10. Relationship to the FieldFlow prototype

The prototype intentionally proves reusable connector mechanics (identity, inbox/outbox, idempotency, ordering, DLQ/replay, circuit/health, audit). Accounting should **reuse those patterns** behind new capability ports — not rebuild reliability from scratch — while adding finance-domain rules the field-service mock does not encode.
