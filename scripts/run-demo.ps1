# Local five-minute demo driver (Prompt 13). Windows PowerShell variant.
# No secrets; no internet. Prefer WSL/bash script when available.
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$env:MOCK_URL = if ($env:MOCK_URL) { $env:MOCK_URL } else { "http://127.0.0.1:5210" }
$env:API_URL = if ($env:API_URL) { $env:API_URL } else { "http://127.0.0.1:5203" }
$env:DEMO_OUT_DIR = if ($env:DEMO_OUT_DIR) { $env:DEMO_OUT_DIR } else { Join-Path $Root "artifacts/demo" }
$env:START_HOSTS = if ($env:START_HOSTS) { $env:START_HOSTS } else { "1" }
$env:CIRCUIT_WAIT_SECONDS = if ($env:CIRCUIT_WAIT_SECONDS) { $env:CIRCUIT_WAIT_SECONDS } else { "16" }

$bash = Get-Command bash -ErrorAction SilentlyContinue
if (-not $bash) {
    Write-Error "bash is required to run scripts/run-demo.sh (Git Bash or WSL)."
}

& bash (Join-Path $PSScriptRoot "run-demo.sh")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
