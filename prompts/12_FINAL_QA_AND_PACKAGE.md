# Cursor Prompt 12 - Final Traceability, Clean Rehearsal, and Submission Package

---

Perform final release QA and create the exact PRG submission ZIP. Do not change architecture or add features unless a release-blocking defect is found.

## Inspect first

Read the original assignment, Definition of Done, traceability matrix, final documents, source, test output, and packaging rules. Produce a release-blocker list before making changes.

## 1. Traceability audit

- Verify every assignment bullet has actual evidence.
- Behavioral requirements must point to test names or reproducible demo steps.
- Update `docs/assignment/requirements-traceability.md` to final status.
- Confirm all assumptions and limitations are consistent across architecture, README, leadership, and AI/scope notes.

## 2. Clean build/test rehearsal

In a clean temporary copy or clean checkout that does not disturb user work:

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --logger "trx;LogFileName=prg-tests.trx"
```

Then follow `03_README.md` exactly to:

- Initialize/reset local persistence.
- Start FieldFlow mock.
- Start connector.
- Import contractors and work orders.
- Repeat sync and prove no duplicate.
- Send a valid and duplicate webhook.
- Send an older status after a newer status.
- Demonstrate unknown-contractor resolution.
- Demonstrate provider outage/circuit open/recovery.
- Inspect connector health, audit, and replay.

Fix README or code if the documented commands disagree. Re-run affected gates.

## 3. Security and hygiene inspection

Search tracked/package-bound files for:

- API keys, bearer tokens, webhook secrets, private keys, connection strings with credentials, email addresses, phone numbers, and real customer names.
- Raw webhook/request payload logs.
- Machine-specific absolute paths.
- TODO/FIXME/placeholders that affect required behavior.
- `.env`, local databases, logs, test output, IDE state, `bin`, and `obj`.

Review findings manually; do not assume every regex match is a leak. Ensure `.env.example` contains placeholder values only.

## 4. PDF and document inspection

- Open/render every page of both PDFs.
- Verify page count, orientation, typography, tables, diagrams, hyperlinks, headings, and page breaks.
- Confirm file names exactly match the assignment.
- Confirm `05_AI_and_Scope_Notes.md` renders as plain readable Markdown.
- Confirm `03_README.md` is standalone and uses relative paths inside the ZIP.

## 5. Assemble staging structure

Create a clean staging directory with exactly:

```text
01_Architecture.pdf
02_Prototype/
03_README.md
04_Leadership_Recommendation.pdf
05_AI_and_Scope_Notes.md
06_Demo.mp4                 # only if completed and <= 5 minutes
```

`02_Prototype` includes source, tests, solution/build/package files, scripts, ADRs, and mock. It excludes Cursor rules/prompts, the assignment PDF, `.git`, build output, local data, test output, and secrets.

## 6. Create and inspect ZIP

Create:

`Jordaine_Gayle_PRG_Integration_Assignment.zip`

Then:

- List every ZIP entry.
- Confirm there is no extra top-level wrapper directory unless intentionally required.
- Extract into a new temporary location.
- Run at least restore/build/test or verify the extracted tree matches the clean-rehearsed tree.
- Confirm the ZIP opens successfully and file sizes are reasonable.
- Optionally record SHA-256 for transfer integrity.

Do not delete the working repository or overwrite user-owned files during staging.

## Release blockers

The following block packaging:

- Failing/ignored required test.
- Missing required artifact.
- Canonical schema drift.
- Unhandled duplicate/out-of-order behavior.
- Blind POST retry.
- Broken README command.
- Secret/real-data exposure.
- Broken/clipped PDF.
- ZIP naming/structure error.
- Demo longer than five minutes.

## Completion report

Return:

1. Exact ZIP path and size.
2. Artifact and top-level entry list.
3. Build/test results.
4. Runtime scenarios rehearsed.
5. Security/hygiene results.
6. PDF visual-QA results and page counts.
7. Traceability status.
8. Known limitations disclosed in the submission.
9. SHA-256 if calculated.

Stop after the inspected ZIP is ready. Do not send email or upload externally.
