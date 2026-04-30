# ============================================================
# OpenFramework Core — script de setup guide (Windows / PowerShell)
# ============================================================
# Usage : depuis la racine du repo, dans PowerShell :
#   .\scripts\setup.ps1
#
# Si tu as un erreur de policy, lance d'abord :
#   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
#
# Equivalent de scripts/setup.sh adapte Windows.
# ============================================================

$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Text)
    Write-Host ""
    Write-Host "=== $Text ===" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Text)
    Write-Host "[OK] $Text" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Text)
    Write-Host "[!]  $Text" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Text)
    Write-Host "[X]  $Text" -ForegroundColor Red
}

function Test-Cmd {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function New-RandomBase64 {
    param([int]$Bytes = 64)
    $buf = New-Object byte[] $Bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buf)
    return [System.Convert]::ToBase64String($buf)
}

function New-RandomHex {
    param([int]$Bytes = 32)
    $buf = New-Object byte[] $Bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buf)
    return -join ($buf | ForEach-Object { $_.ToString("x2") })
}

Write-Host ""
Write-Host "OpenFramework Core - Setup (Windows)" -ForegroundColor Cyan
Write-Host ""

# ── Verification des prerequis ─────────────────────────────────
Write-Section "1. Verification des prerequis"

$missing = 0

# Docker
if (Test-Cmd "docker") {
    $ver = (docker --version 2>$null) | Select-Object -First 1
    Write-Ok "Docker installe : $ver"
    try {
        docker info 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "Docker installe mais le daemon ne tourne pas. Lance Docker Desktop."
            $missing++
        }
    } catch {
        Write-Warn "Docker installe mais le daemon ne tourne pas. Lance Docker Desktop."
        $missing++
    }
} else {
    Write-Err "Docker manquant"
    Write-Host "    Installe Docker Desktop : https://docs.docker.com/desktop/install/windows-install/"
    $missing++
}

# Docker Compose v2
$composeOk = $false
try {
    docker compose version 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $ver = (docker compose version 2>$null) | Select-Object -First 1
        Write-Ok "Docker Compose : $ver"
        $composeOk = $true
    }
} catch {}

if (-not $composeOk) {
    Write-Err "Docker Compose v2 manquant"
    Write-Host "    Inclus dans Docker Desktop recent. Met a jour Docker Desktop."
    $missing++
}

# Pas besoin d'OpenSSL : on utilise les API .NET via PowerShell

if ($missing -gt 0) {
    Write-Host ""
    Write-Err "$missing prerequis manquant(s). Installe-les puis relance ce script."
    exit 1
}

# ── Generer .env ────────────────────────────────────────────
Write-Section "2. Configuration .env"

$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot ".env"
$envExample = Join-Path $repoRoot ".env.example"

if (Test-Path $envFile) {
    Write-Warn ".env existe deja. On le garde tel quel."
    Write-Host "    Si tu veux tout regenerer : Remove-Item .env"
} else {
    Write-Host "Generation de .env depuis .env.example..."
    Copy-Item $envExample $envFile

    # Generer les secrets
    $jwtKey = New-RandomBase64 -Bytes 64
    $serverSecret = New-RandomHex -Bytes 32
    $sessionSecret = New-RandomHex -Bytes 32
    $mssqlSuffix = New-RandomHex -Bytes 8
    $mssqlPwd = "OpenFw${mssqlSuffix}A1!"

    # Lire et patcher
    $content = Get-Content $envFile -Raw
    $content = $content -replace '(?m)^MSSQL_SA_PASSWORD=.*$', "MSSQL_SA_PASSWORD=$mssqlPwd"
    $content = $content -replace '(?m)^SERVER_SECRET=.*$', "SERVER_SECRET=$serverSecret"
    $content = $content -replace '(?m)^GAME_SERVER_SECRET=.*$', "GAME_SERVER_SECRET=$serverSecret"
    $content = $content -replace '(?m)^JWT_KEY=.*$', "JWT_KEY=$jwtKey"
    $content = $content -replace '(?m)^SESSION_SECRET=.*$', "SESSION_SECRET=$sessionSecret"

    Set-Content -Path $envFile -Value $content -Encoding UTF8 -NoNewline
    Write-Ok "Secrets generes (MSSQL_SA_PASSWORD, JWT_KEY, SERVER_SECRET, SESSION_SECRET)"

    # Steam API key
    Write-Host ""
    Write-Host "Pour le website (devblog/admin), il te faut une cle API Steam."
    Write-Host "Obtiens-la sur : https://steamcommunity.com/dev/apikey"
    $steamKey = Read-Host "STEAM_API_KEY (laisse vide pour skip)"
    if ($steamKey) {
        $content = Get-Content $envFile -Raw
        $content = $content -replace '(?m)^STEAM_API_KEY=.*$', "STEAM_API_KEY=$steamKey"
        Set-Content -Path $envFile -Value $content -Encoding UTF8 -NoNewline
        Write-Ok "STEAM_API_KEY enregistree"
    }

    # Steam ID admin
    Write-Host ""
    Write-Host "Ton SteamID64 (admin du panel web). Trouve-le sur : https://steamid.io"
    $steamId = Read-Host "ALLOWED_STEAM_IDS (laisse vide pour skip)"
    if ($steamId) {
        $content = Get-Content $envFile -Raw
        $content = $content -replace '(?m)^ALLOWED_STEAM_IDS=.*$', "ALLOWED_STEAM_IDS=$steamId"
        Set-Content -Path $envFile -Value $content -Encoding UTF8 -NoNewline
        Write-Ok "ALLOWED_STEAM_IDS enregistree"
    }
}

# ── Lancer docker compose ───────────────────────────────────
Write-Section "3. Demarrage des services"

$confirm = Read-Host "Lancer docker compose up -d maintenant ? [Y/n]"
if ([string]::IsNullOrEmpty($confirm)) { $confirm = "Y" }

if ($confirm -match '^[YyOo]') {
    Push-Location $repoRoot
    try {
        docker compose up -d
        Write-Host ""
        Write-Ok "Services demarres."
        Write-Host ""
        Write-Host "URLs accessibles :"
        Write-Host "  - API du jeu  : http://localhost:8443"
        Write-Host "  - Swagger     : http://localhost:8443/swagger"
        Write-Host "  - Adminer     : http://localhost:8080"
        Write-Host "  - Site web    : http://localhost:4173"
        Write-Host "  - Devblog API : http://localhost:3001"
        Write-Host ""
        Write-Host "Logs en direct :  docker compose logs -f"
        Write-Host "Stopper        :  docker compose down"
        Write-Host ""
        Write-Host "Suite : voir docs/SETUP.md pour configurer le serveur s&box dedie."
    } finally {
        Pop-Location
    }
} else {
    Write-Host "OK, lance manuellement quand tu es pret : docker compose up -d"
}
