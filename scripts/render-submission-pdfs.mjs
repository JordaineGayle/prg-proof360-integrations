#!/usr/bin/env node
/**
 * Render Prompt 11 PDFs from Markdown sources via Chrome headless.
 * Usage: node scripts/render-submission-pdfs.mjs
 */
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";
import {
  mkdirSync,
  readFileSync,
  writeFileSync,
  copyFileSync,
  existsSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { tmpdir } from "node:os";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, "..");
const outDir = join(root, "docs", "packages");
const css = readFileSync(join(root, "scripts", "pdf", "print.css"), "utf8");

const markedTgz = execFileSync("npm", ["pack", "marked@18.0.6", "--pack-destination", tmpdir()], {
  cwd: root,
  encoding: "utf8",
}).trim().split("\n").pop();
const extractDir = join(tmpdir(), `marked-extract-${Date.now()}`);
mkdirSync(extractDir, { recursive: true });
execFileSync("tar", ["-xzf", join(tmpdir(), markedTgz), "-C", extractDir]);
const require = createRequire(join(extractDir, "package", "package.json"));
const { marked } = require("marked");

function toHtml(title, markdown) {
  const body = marked.parse(markdown, { async: false });
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<title>${title}</title>
<style>${css}</style>
</head>
<body>
${body}
</body>
</html>`;
}

function chromePrint(htmlPath, pdfPath) {
  const chrome =
    process.env.CHROME_PATH ||
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
  if (!existsSync(chrome)) {
    throw new Error(`Chrome not found at ${chrome}. Set CHROME_PATH.`);
  }
  execFileSync(
    chrome,
    [
      "--headless=new",
      "--disable-gpu",
      "--no-pdf-header-footer",
      `--print-to-pdf=${pdfPath}`,
      `file://${htmlPath}`,
    ],
    { stdio: "inherit" },
  );
}

mkdirSync(outDir, { recursive: true });

const jobs = [
  {
    title: "01 Architecture",
    md: join(root, "docs", "architecture", "architecture.md"),
    pdf: join(outDir, "01_Architecture.pdf"),
    html: join(outDir, "01_Architecture.print.html"),
  },
  {
    title: "04 Leadership Recommendation",
    md: join(root, "docs", "leadership", "leadership-recommendation.md"),
    pdf: join(outDir, "04_Leadership_Recommendation.pdf"),
    html: join(outDir, "04_Leadership_Recommendation.print.html"),
  },
];

for (const job of jobs) {
  const md = readFileSync(job.md, "utf8");
  const html = toHtml(job.title, md);
  writeFileSync(job.html, html, "utf8");
  chromePrint(job.html, job.pdf);
  console.log(`Wrote ${job.pdf}`);
}

copyFileSync(
  join(root, "docs", "assignment", "ai-and-scope-notes.md"),
  join(outDir, "05_AI_and_Scope_Notes.md"),
);
console.log(`Wrote ${join(outDir, "05_AI_and_Scope_Notes.md")}`);
