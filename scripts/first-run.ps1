# ============================================================
# OpenFramework Core — first run (Windows / PowerShell)
# ============================================================
# Usage : .\scripts\first-run.ps1
# Lance docker compose, attend que le frontend soit pret, puis ouvre
# automatiquement le browser sur le wizard d'installation.
#
# Si erreur de policy : Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
# ============================================================

$ErrorActionPreference = "Stop"

function Write-Ok    { param([string]$T) Write-Host "[OK] $T" -ForegroundColor Green }
function Write-Warn  { param([string]$T) Write-Host "[!]  $T" -ForegroundColor Yellow }
function Write-Err   { param([string]$T) Write-Host "[X]  $T" -ForegroundColor Red }
function Write-Step  { param([string]$T) Write-Host ""; Write-Host $T -ForegroundColor Cyan }

$repoRoot   = Split-Path -Parent $PSScriptRoot
$wizardUrl  = "http://localhost:4173"

Write-Host ""
Write-Host "OpenFramework Core - First Run" -ForegroundColor Cyan
Write-Host ""

# Verifier Docker
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Err "Docker n'est pas installe."
    Write-Host "    Installe Docker Desktop : https://docs.docker.com/desktop/install/windows-install/"
    exit 1
}

try {
    docker info 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "docker info failed" }
} catch {
    Write-Err "Docker est installe mais le daemon ne tourne pas."
    Write-Host "    Lance Docker Desktop puis relance ce script."
    exit 1
}

Push-Location $repoRoot

try {
    Write-Step "1. Lancement des services (docker compose up -d)..."
    docker compose up -d

    Write-Step "2. Attente que le frontend reponde..."
    $waitMax = 60
    $ready = $false
    for ($i = 1; $i -le $waitMax; $i++) {
        try {
            $r = Invoke-WebRequest -Uri $wizardUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($r.StatusCode -eq 200 -or $r.StatusCode -eq 304) {
                Write-Ok "Frontend pret apres ${i}s"
                $ready = $true
                break
            }
        } catch {}
        Start-Sleep -Seconds 1
    }
    if (-not $ready) {
        Write-Warn "Le frontend n'a pas repondu apres ${waitMax}s."
        Write-Host "  Verifie : docker compose logs website.frontend"
        Write-Host "  Tu peux quand meme ouvrir : $wizardUrl"
    }

    Write-Step "3. Ouverture du wizard dans le browser..."
    Start-Process $wizardUrl

    Write-Host ""
    Write-Host "OpenFramework Core est lance." -ForegroundColor Green
    Write-Host ""
    Write-Host "URLs :"
    Write-Host "  - Wizard / Site web : $wizardUrl"
    Write-Host "  - API du jeu        : http://localhost:8443"
    Write-Host "  - Adminer (DB)      : http://localhost:8080"
    Write-Host ""
    Write-Host "Logs en direct :  docker compose logs -f"
    Write-Host "Stopper        :  docker compose down"
} finally {
    Pop-Location
}
