#!/usr/bin/env bash
# ============================================================
# OpenFramework Core — script de setup guide
# ============================================================
# Usage : depuis la racine du repo, `bash scripts/setup.sh`
# Compatible : Linux, macOS, WSL, Git Bash sur Windows.
#
# Ce que fait le script :
#   1. Verifie chaque prerequis (docker, docker compose, openssl)
#   2. Pour ceux qui manquent : affiche les liens d'install
#   3. Genere un .env depuis .env.example avec des secrets aleatoires
#   4. Demande les valeurs manuelles (Steam API key, SteamID admin)
#   5. Lance `docker compose up -d` si tu confirmes
# ============================================================

set -e

# Couleurs (desactivees si pas de TTY)
if [ -t 1 ]; then
    BOLD="$(tput bold 2>/dev/null || echo '')"
    GREEN="$(tput setaf 2 2>/dev/null || echo '')"
    YELLOW="$(tput setaf 3 2>/dev/null || echo '')"
    RED="$(tput setaf 1 2>/dev/null || echo '')"
    BLUE="$(tput setaf 4 2>/dev/null || echo '')"
    RESET="$(tput sgr0 2>/dev/null || echo '')"
else
    BOLD=""; GREEN=""; YELLOW=""; RED=""; BLUE=""; RESET=""
fi

# Detecter l'OS
detect_os() {
    case "$(uname -s)" in
        Linux*)     echo "linux" ;;
        Darwin*)    echo "macos" ;;
        MINGW*|MSYS*|CYGWIN*) echo "windows" ;;
        *)          echo "unknown" ;;
    esac
}

OS=$(detect_os)

echo ""
echo "${BOLD}${BLUE}=== OpenFramework Core — Setup ===${RESET}"
echo "OS detecte : ${BOLD}$OS${RESET}"
echo ""

# Compteur de blockers
MISSING=0

# ── Verifier Docker ─────────────────────────────────────────
check_docker() {
    if command -v docker > /dev/null 2>&1; then
        local ver
        ver=$(docker --version 2>/dev/null | head -n1)
        echo "${GREEN}✓${RESET} Docker installe : ${ver}"
        if ! docker info > /dev/null 2>&1; then
            echo "  ${YELLOW}⚠${RESET}  Docker est installe mais ne tourne pas. Lance Docker Desktop / le service docker."
            MISSING=$((MISSING + 1))
        fi
    else
        echo "${RED}✗${RESET} Docker manquant"
        case "$OS" in
            windows) echo "    Installe Docker Desktop : https://docs.docker.com/desktop/install/windows-install/" ;;
            macos)   echo "    Installe Docker Desktop : https://docs.docker.com/desktop/install/mac-install/" ;;
            linux)   echo "    Installe Docker Engine : https://docs.docker.com/engine/install/" ;;
            *)       echo "    Voir : https://docs.docker.com/get-docker/" ;;
        esac
        MISSING=$((MISSING + 1))
    fi
}

# ── Verifier Docker Compose ─────────────────────────────────
check_docker_compose() {
    if docker compose version > /dev/null 2>&1; then
        local ver
        ver=$(docker compose version 2>/dev/null | head -n1)
        echo "${GREEN}✓${RESET} Docker Compose : ${ver}"
    elif command -v docker-compose > /dev/null 2>&1; then
        echo "${YELLOW}⚠${RESET}  Docker Compose v1 detecte (deprecate). Met a jour vers v2 (inclus dans Docker Desktop recent)."
    else
        echo "${RED}✗${RESET} Docker Compose manquant"
        echo "    Inclus dans Docker Desktop. Si tu utilises Docker Engine seul :"
        echo "    https://docs.docker.com/compose/install/"
        MISSING=$((MISSING + 1))
    fi
}

# ── Verifier OpenSSL (pour generer les secrets) ─────────────
check_openssl() {
    if command -v openssl > /dev/null 2>&1; then
        echo "${GREEN}✓${RESET} OpenSSL : $(openssl version 2>/dev/null | head -n1)"
    else
        echo "${YELLOW}⚠${RESET}  OpenSSL manquant — necessaire pour generer les secrets aleatoires"
        case "$OS" in
            windows) echo "    Generalement inclus avec Git for Windows. Sinon : https://slproweb.com/products/Win32OpenSSL.html" ;;
            macos)   echo "    brew install openssl" ;;
            linux)   echo "    sudo apt install openssl  (ou yum/pacman selon distro)" ;;
        esac
        MISSING=$((MISSING + 1))
    fi
}

echo "${BOLD}1. Verification des prerequis${RESET}"
echo "─────────────────────────────"
check_docker
check_docker_compose
check_openssl
echo ""

if [ $MISSING -gt 0 ]; then
    echo "${RED}${BOLD}$MISSING prerequis manquant(s).${RESET} Installe-les puis relance ce script."
    exit 1
fi

# ── 2. Generer .env si absent ───────────────────────────────
echo "${BOLD}2. Configuration .env${RESET}"
echo "─────────────────────"

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
ENV_EXAMPLE="$REPO_ROOT/.env.example"

if [ -f "$ENV_FILE" ]; then
    echo "${YELLOW}⚠${RESET}  .env existe deja. On le garde tel quel."
    echo "    Si tu veux tout regenerer, supprime-le d'abord : rm .env"
else
    echo "Generation de .env depuis .env.example..."
    cp "$ENV_EXAMPLE" "$ENV_FILE"

    # Remplir les secrets aleatoires
    JWT_KEY=$(openssl rand -base64 64 | tr -d '\n')
    SERVER_SECRET=$(openssl rand -hex 32)
    SESSION_SECRET=$(openssl rand -hex 32)
    MSSQL_PWD="OpenFw$(openssl rand -hex 8)A1!"

    # macOS sed -i besoin de '' apres -i
    if [ "$OS" = "macos" ]; then
        SED_INPLACE=(-i '')
    else
        SED_INPLACE=(-i)
    fi

    sed "${SED_INPLACE[@]}" "s|^MSSQL_SA_PASSWORD=.*|MSSQL_SA_PASSWORD=$MSSQL_PWD|" "$ENV_FILE"
    sed "${SED_INPLACE[@]}" "s|^SERVER_SECRET=.*|SERVER_SECRET=$SERVER_SECRET|" "$ENV_FILE"
    sed "${SED_INPLACE[@]}" "s|^GAME_SERVER_SECRET=.*|GAME_SERVER_SECRET=$SERVER_SECRET|" "$ENV_FILE"
    sed "${SED_INPLACE[@]}" "s|^JWT_KEY=.*|JWT_KEY=$JWT_KEY|" "$ENV_FILE"
    sed "${SED_INPLACE[@]}" "s|^SESSION_SECRET=.*|SESSION_SECRET=$SESSION_SECRET|" "$ENV_FILE"

    echo "${GREEN}✓${RESET} Secrets generes (MSSQL_SA_PASSWORD, JWT_KEY, SERVER_SECRET, SESSION_SECRET)"

    # Demander Steam API key (manuel)
    echo ""
    echo "Pour le website (devblog/admin), il te faut une cle API Steam."
    echo "Obtiens-la sur : ${BLUE}https://steamcommunity.com/dev/apikey${RESET}"
    printf "STEAM_API_KEY (laisse vide pour skip) : "
    read -r STEAM_KEY
    if [ -n "$STEAM_KEY" ]; then
        sed "${SED_INPLACE[@]}" "s|^STEAM_API_KEY=.*|STEAM_API_KEY=$STEAM_KEY|" "$ENV_FILE"
        echo "${GREEN}✓${RESET} STEAM_API_KEY enregistree"
    fi

    # Demander SteamID admin
    echo ""
    echo "Ton SteamID64 (admin du panel web). Trouve-le sur : ${BLUE}https://steamid.io${RESET}"
    printf "ALLOWED_STEAM_IDS (laisse vide pour skip) : "
    read -r STEAM_ID
    if [ -n "$STEAM_ID" ]; then
        sed "${SED_INPLACE[@]}" "s|^ALLOWED_STEAM_IDS=.*|ALLOWED_STEAM_IDS=$STEAM_ID|" "$ENV_FILE"
        echo "${GREEN}✓${RESET} ALLOWED_STEAM_IDS enregistree"
    fi
fi
echo ""

# ── 3. Lancer docker compose ────────────────────────────────
echo "${BOLD}3. Demarrage des services${RESET}"
echo "─────────────────────────"
printf "Lancer docker compose up -d maintenant ? [Y/n] "
read -r CONFIRM
CONFIRM=${CONFIRM:-Y}

if [[ "$CONFIRM" =~ ^[YyOo] ]]; then
    cd "$REPO_ROOT"
    docker compose up -d
    echo ""
    echo "${GREEN}${BOLD}✓ Services demarres.${RESET}"
    echo ""
    echo "URLs accessibles :"
    echo "  - API du jeu : ${BLUE}http://localhost:8443${RESET}"
    echo "  - Swagger    : ${BLUE}http://localhost:8443/swagger${RESET}"
    echo "  - Adminer    : ${BLUE}http://localhost:8080${RESET}"
    echo "  - Site web   : ${BLUE}http://localhost:4173${RESET}"
    echo "  - Devblog API: ${BLUE}http://localhost:3001${RESET}"
    echo ""
    echo "Logs en direct :  docker compose logs -f"
    echo "Stopper      :    docker compose down"
    echo ""
    echo "Suite : voir ${BOLD}docs/SETUP.md${RESET} pour configurer le serveur s&box dedie."
else
    echo "OK, lance manuellement quand tu es pret : ${BOLD}docker compose up -d${RESET}"
fi
