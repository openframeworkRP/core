#!/usr/bin/env bash
# ============================================================
# OpenFramework Core — first run (lance + ouvre le wizard browser)
# ============================================================
# Usage : bash scripts/first-run.sh
# Lance docker compose, attend que le frontend soit pret, puis ouvre
# automatiquement le browser sur le wizard d'installation.
# Compatible : Linux, macOS, WSL, Git Bash sur Windows.
# ============================================================

set -e

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

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WIZARD_URL="http://localhost:4173"

echo ""
echo "${BOLD}${BLUE}=== OpenFramework Core — First Run ===${RESET}"
echo ""

# Verifier que Docker est dispo
if ! command -v docker > /dev/null 2>&1; then
    echo "${RED}Docker n'est pas installe.${RESET}"
    echo "Installe Docker Desktop puis relance ce script :"
    echo "  https://docs.docker.com/get-docker/"
    exit 1
fi

if ! docker info > /dev/null 2>&1; then
    echo "${RED}Docker est installe mais le daemon ne tourne pas.${RESET}"
    echo "Lance Docker Desktop / le service docker, puis relance."
    exit 1
fi

cd "$REPO_ROOT"

echo "${BOLD}1.${RESET} Lancement des services (docker compose up -d)…"
docker compose up -d

echo ""
echo "${BOLD}2.${RESET} Attente que le frontend reponde…"
WAIT_MAX=60
for i in $(seq 1 $WAIT_MAX); do
    if curl -fs -o /dev/null "$WIZARD_URL" 2>/dev/null; then
        echo "${GREEN}✓ Frontend pret apres ${i}s${RESET}"
        break
    fi
    if [ $i -eq $WAIT_MAX ]; then
        echo "${YELLOW}⚠ Le frontend n'a pas repondu apres ${WAIT_MAX}s.${RESET}"
        echo "  Verifie l'etat : docker compose logs website.frontend"
        echo "  Tu peux quand meme ouvrir : $WIZARD_URL"
        break
    fi
    sleep 1
done

echo ""
echo "${BOLD}3.${RESET} Ouverture du wizard dans le browser…"

case "$(uname -s)" in
    Linux*)
        if command -v xdg-open > /dev/null 2>&1; then
            xdg-open "$WIZARD_URL" > /dev/null 2>&1 &
        elif command -v wslview > /dev/null 2>&1; then
            wslview "$WIZARD_URL" > /dev/null 2>&1 &
        else
            echo "${YELLOW}⚠ xdg-open absent — ouvre manuellement : $WIZARD_URL${RESET}"
        fi
        ;;
    Darwin*)
        open "$WIZARD_URL"
        ;;
    MINGW*|MSYS*|CYGWIN*)
        start "$WIZARD_URL"
        ;;
    *)
        echo "${YELLOW}⚠ OS inconnu — ouvre manuellement : $WIZARD_URL${RESET}"
        ;;
esac

echo ""
echo "${GREEN}${BOLD}OpenFramework Core est lance.${RESET}"
echo ""
echo "URLs :"
echo "  - Wizard / Site web : ${BLUE}${WIZARD_URL}${RESET}"
echo "  - API du jeu        : ${BLUE}http://localhost:8443${RESET}"
echo "  - Adminer (DB)      : ${BLUE}http://localhost:8080${RESET}"
echo ""
echo "Logs en direct :  docker compose logs -f"
echo "Stopper        :  docker compose down"
