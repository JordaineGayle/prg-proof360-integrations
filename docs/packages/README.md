# Submission package staging (Prompt 11)

| File | Role |
|---|---|
| `01_Architecture.pdf` | Architecture deliverable (source: `../architecture/architecture.md`) |
| `03_README.md` | Standalone submission README |
| `04_Leadership_Recommendation.pdf` | Accounting-first memo (source: `../leadership/leadership-recommendation.md`) |
| `05_AI_and_Scope_Notes.md` | AI use + scope (mirrored from `../assignment/ai-and-scope-notes.md`) |

Regenerate PDFs:

```bash
node scripts/render-submission-pdfs.mjs
```

Requires Google Chrome (`CHROME_PATH` override supported). Print HTML intermediates (`*.print.html`) are rebuild artifacts and need not be packaged in the final ZIP.
