#!/bin/bash
# Restaure un backup
# Usage: ./scripts/restore.sh backups/backup_2026-03-27_12-00-00.tar.gz

set -e

if [ -z "$1" ]; then
  echo "❌ Usage: $0 <fichier_backup.tar.gz>"
  exit 1
fi

BACKUP_FILE="$1"

if [ ! -f "$BACKUP_FILE" ]; then
  echo "❌ Fichier introuvable : $BACKUP_FILE"
  exit 1
fi

echo "⚠️  Cette opération va écraser les données actuelles."
read -p "Continuer ? (oui/non) : " CONFIRM
if [ "$CONFIRM" != "oui" ]; then
  echo "Annulé."
  exit 0
fi

TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

echo "📂 Extraction de $BACKUP_FILE ..."
tar -xzf "$BACKUP_FILE" -C "$TEMP_DIR"

EXTRACTED=$(ls "$TEMP_DIR")

# Restaure SQLite
echo "  → Restauration de la base de données..."
docker run --rm \
  -v site_web_devblog_data:/data \
  -v "$TEMP_DIR/$EXTRACTED/data":/backup \
  alpine sh -c "rm -rf /data/* && cp -r /backup/. /data/"

# Restaure uploads
echo "  → Restauration des fichiers uploadés..."
docker run --rm \
  -v site_web_devblog_uploads:/uploads \
  -v "$TEMP_DIR/$EXTRACTED/uploads":/backup \
  alpine sh -c "rm -rf /uploads/* && cp -r /backup/. /uploads/"

echo "✅ Restauration terminée. Redémarre les conteneurs si nécessaire."
