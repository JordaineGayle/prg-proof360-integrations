# Submission package staging (Prompt 11)

| File | Role |
|---|---|
| `01_Architecture.pdf` | Architecture deliverable (source: `../architecture/architecture.md`) |
| `03_README.md` | Standalone submission README |
| `04_Leadership_Recommendation.pdf` | Accounting-first memo (source: `../leadership/leadership-recommendation.md`) |
| `05_AI_and_Scope_Notes.md` | AI use + scope (mirrored from `../assignment/ai-and-scope-notes.md`) |
| `06_Demo.mp4` | Optional ≤5 min demo (Prompt 13) |
| `Jordaine_Gayle_PRG_Integration_Assignment.zip` | Final submission package |

Regenerate PDFs:

```bash
node scripts/render-submission-pdfs.mjs
```

Regenerate demo capture + video:

```bash
./scripts/run-demo.sh
./scripts/render-demo-video.sh
```

Regenerate ZIP:

```bash
./scripts/package-submission.sh
```

Requires Google Chrome for PDFs (`CHROME_PATH` override supported). Print HTML intermediates (`*.print.html`) are rebuild artifacts and are not included in the ZIP.


## Submission ZIP (Prompt 12)

`Jordaine_Gayle_PRG_Integration_Assignment.zip` — regenerate with `./scripts/package-submission.sh`.
