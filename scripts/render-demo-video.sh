#!/usr/bin/env bash
# Render captioned demo MP4 from artifacts/demo/steps.json (Prompt 13).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${DEMO_OUT_DIR:-$ROOT/artifacts/demo}"
STEPS_JSON="${STEPS_JSON:-$OUT_DIR/steps.json}"
WORK="$OUT_DIR/render"
MP4_OUT="${DEMO_MP4_OUT:-$ROOT/docs/packages/06_Demo.mp4}"
VOICE="${DEMO_VOICE:-Samantha}"

if [[ ! -f "$STEPS_JSON" ]]; then
  echo "Missing $STEPS_JSON — run scripts/run-demo.sh first." >&2
  exit 1
fi

command -v ffmpeg >/dev/null
command -v ffprobe >/dev/null
command -v python3 >/dev/null
command -v say >/dev/null

rm -rf "$WORK"
mkdir -p "$WORK/audio" "$WORK/clips" "$WORK/png" "$(dirname "$MP4_OUT")"

python3 - "$STEPS_JSON" "$WORK" "$VOICE" "$MP4_OUT" <<'PY'
import json
import subprocess
import textwrap
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

steps_path = Path(__import__("sys").argv[1])
work = Path(__import__("sys").argv[2])
voice = __import__("sys").argv[3]
mp4_out = Path(__import__("sys").argv[4])

audio_dir = work / "audio"
clips_dir = work / "clips"
png_dir = work / "png"

steps = json.loads(steps_path.read_text())
weights = {
    "00": 6, "01": 18, "02": 12, "03": 10, "04": 12, "05": 12, "06": 14,
    "07": 12, "08": 12, "09": 16, "10": 10, "11": 12, "12": 16,
    "13": 10, "14": 16, "15": 12, "16": 8, "17": 14, "18": 20,
}

try:
    font_title = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Bold.ttf", 36)
    font_body = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 22)
    font_mono = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 18)
except OSError:
    font_title = ImageFont.load_default()
    font_body = font_title
    font_mono = font_title

manifest = []
for i, step in enumerate(steps):
    sid = step["id"]
    title = step["title"]
    narration = step["narration"]
    output = (step.get("output") or "").strip().replace("\t", " ")
    if len(output) > 900:
        output = output[:900] + "\n…"

    img = Image.new("RGB", (1280, 720), (17, 24, 39))
    draw = ImageDraw.Draw(img)
    draw.text((48, 36), f"PRG FieldFlow Connector Demo  ·  Step {sid}", fill=(156, 163, 175), font=font_mono)
    draw.text((48, 80), title, fill=(255, 255, 255), font=font_title)

    y = 140
    for line in textwrap.wrap(narration, width=78):
        draw.text((48, y), line, fill=(229, 231, 235), font=font_body)
        y += 28

    y += 16
    draw.text((48, y), "Evidence", fill=(125, 211, 252), font=font_body)
    y += 34
    for line in output.splitlines():
        for wrapped in textwrap.wrap(line, width=96) or [""]:
            if y > 660:
                draw.text((48, y), "…", fill=(156, 163, 175), font=font_mono)
                y = 999
                break
            draw.text((48, y), wrapped, fill=(209, 213, 219), font=font_mono)
            y += 22
        if y > 660:
            break

    png = png_dir / f"s_{i:02d}.png"
    img.save(png)

    aiff = audio_dir / f"n_{i:02d}.aiff"
    spoken = f"Step {sid}. {title}. {narration}"
    if len(spoken) > 280:
        spoken = spoken[:277] + "..."
    subprocess.run(["say", "-v", voice, "-o", str(aiff), spoken], check=True)
    probe = subprocess.check_output(
        [
            "ffprobe", "-v", "error", "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1:nokey=1", str(aiff),
        ],
        text=True,
    ).strip()
    audio_dur = float(probe or "1")
    dur = max(float(weights.get(sid, 12)), audio_dur + 0.5)
    dur = min(dur, 22.0)
    manifest.append({"i": i, "dur": dur, "aiff": aiff, "png": png})

total = sum(m["dur"] for m in manifest)
budget = 285.0
if total > budget:
    scale = budget / total
    for m in manifest:
        m["dur"] *= scale

concat_list = work / "concat.txt"
with concat_list.open("w", encoding="utf-8") as cf:
    for m in manifest:
        i = m["i"]
        clip = clips_dir / f"c_{i:02d}.mp4"
        subprocess.run(
            [
                "ffmpeg", "-y",
                "-loop", "1", "-i", str(m["png"]),
                "-i", str(m["aiff"]),
                "-c:v", "libx264", "-tune", "stillimage", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "128k",
                "-t", f"{m['dur']:.3f}",
                "-shortest",
                str(clip),
            ],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        cf.write(f"file '{clip}'\n")

subprocess.run(
    ["ffmpeg", "-y", "-f", "concat", "-safe", "0", "-i", str(concat_list), "-c", "copy", str(mp4_out)],
    check=True,
)
probe = subprocess.check_output(
    [
        "ffprobe", "-v", "error", "-show_entries", "format=duration",
        "-of", "default=noprint_wrappers=1:nokey=1", str(mp4_out),
    ],
    text=True,
).strip()
duration = float(probe)
print(f"Wrote {mp4_out} duration={duration:.1f}s slides={len(manifest)}")
if duration > 300:
    raise SystemExit("Demo video exceeds 5 minutes")
PY

ls -lh "$MP4_OUT"
echo -n "duration_seconds="
ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 "$MP4_OUT"
echo
