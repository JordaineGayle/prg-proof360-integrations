#!/usr/bin/env bash
# Assemble Jordaine_Gayle_PRG_Integration_Assignment.zip (Prompt 12).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STAGING_ROOT="${STAGING_ROOT:-$(mktemp -d /tmp/prg-submission-XXXXXX)}"
STAGE="$STAGING_ROOT/stage"
PROTO="$STAGE/02_Prototype"
ZIP_OUT="${ZIP_OUT:-$ROOT/artifacts/submission/Jordaine_Gayle_PRG_Integration_Assignment.zip}"

echo "Staging at: $STAGE"
rm -rf "$STAGE"
mkdir -p "$PROTO" "$(dirname "$ZIP_OUT")"

cp "$ROOT/docs/packages/01_Architecture.pdf" "$STAGE/01_Architecture.pdf"
cp "$ROOT/docs/packages/03_README.md" "$STAGE/03_README.md"
cp "$ROOT/docs/packages/04_Leadership_Recommendation.pdf" "$STAGE/04_Leadership_Recommendation.pdf"
cp "$ROOT/docs/packages/05_AI_and_Scope_Notes.md" "$STAGE/05_AI_and_Scope_Notes.md"
if [[ -f "$ROOT/docs/packages/06_Demo.mp4" ]]; then
  cp "$ROOT/docs/packages/06_Demo.mp4" "$STAGE/06_Demo.mp4"
fi

rsync -a \
  --exclude '.git/' \
  --exclude '.cursor/' \
  --exclude '.idea/' \
  --exclude 'artifacts/' \
  --exclude 'prompts/' \
  --exclude 'reference/' \
  --exclude 'bin/' \
  --exclude 'obj/' \
  --exclude '.vs/' \
  --exclude '*.user' \
  --exclude '*.db' \
  --exclude '*.db-shm' \
  --exclude '*.db-wal' \
  --exclude '.env' \
  --exclude 'TestResults/' \
  --exclude 'coverage/' \
  --exclude '.DS_Store' \
  --exclude '*.zip' \
  --exclude '*.mp4' \
  --exclude 'Jordaine_Gayle_PRG_Integration_Assignment.zip' \
  "$ROOT/" "$PROTO/"

rm -rf \
  "$PROTO/.git" \
  "$PROTO/.cursor" \
  "$PROTO/.idea" \
  "$PROTO/artifacts" \
  "$PROTO/prompts" \
  "$PROTO/reference"

# Submission PDFs/README/ZIP live at ZIP root only (keep architecture source docs)
rm -f \
  "$PROTO/docs/packages/"*.pdf \
  "$PROTO/docs/packages/"*.print.html \
  "$PROTO/docs/packages/"*.zip \
  "$PROTO/docs/packages/"*.mp4 \
  "$PROTO/docs/packages/03_README.md" \
  "$PROTO/docs/packages/05_AI_and_Scope_Notes.md" \
  "$PROTO/"*.zip \
  "$PROTO/"*.mp4

find "$PROTO" \( -name bin -o -name obj -o -name TestResults \) -type d -prune -exec rm -rf {} +
find "$PROTO" \( -name '*.db' -o -name '*.db-shm' -o -name '*.db-wal' -o -name '.env' -o -name '.DS_Store' \) -delete

test -d "$PROTO/docs/decisions"
test -d "$PROTO/scripts"
test -f "$PROTO/PRG.Proof360.Integrations.sln"
test -f "$PROTO/.env.example"
test ! -d "$PROTO/.git"
test ! -d "$PROTO/bin"

rm -f "$ZIP_OUT"
ZIP_ARGS=(
  01_Architecture.pdf
  02_Prototype
  03_README.md
  04_Leadership_Recommendation.pdf
  05_AI_and_Scope_Notes.md
)
if [[ -f "$STAGE/06_Demo.mp4" ]]; then
  ZIP_ARGS+=(06_Demo.mp4)
fi
(
  cd "$STAGE"
  zip -rq "$ZIP_OUT" "${ZIP_ARGS[@]}"
)

echo "ZIP_OUT=$ZIP_OUT"
ls -lh "$ZIP_OUT"
echo "--- top-level ---"
unzip -Z1 "$ZIP_OUT" | awk -F/ '{print $1}' | sort -u
echo "--- entry count ---"
unzip -Z1 "$ZIP_OUT" | wc -l
echo "SHA256=$(shasum -a 256 "$ZIP_OUT" | awk '{print $1}')"
echo "STAGING_ROOT=$STAGING_ROOT"
