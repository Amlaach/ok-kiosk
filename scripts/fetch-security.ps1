param(
    [string]$SshHost = "github-okkiosk-sec"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$vendorDir = Join-Path $repoRoot "vendor\security"
$tmpDir = Join-Path $env:TEMP "okkiosk-sec-bin-$(Get-Random)"

Write-Host "Fetching OkKiosk.Security.dll via unique SSH host ($SshHost)..."
git clone --depth 1 --branch bin "git@${SshHost}:Amlaach/ok-kiosk-security.git" $tmpDir

New-Item -ItemType Directory -Path $vendorDir -Force | Out-Null
Copy-Item (Join-Path $tmpDir "OkKiosk.Security.dll") (Join-Path $vendorDir "OkKiosk.Security.dll") -Force
Remove-Item $tmpDir -Recurse -Force

Write-Host "Done! Binary placed at $vendorDir\OkKiosk.Security.dll (gitignored)."
