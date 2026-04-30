#!/bin/bash
# Backup SQLite data + uploads
# Usage: ./scripts/backup.sh
# Optionnel: ./scripts/backup.sh /chemin/destination

set -e

BACKUP_DIR="${1:-./backups}"
DATE=$(date +%Y-%m-%d_%H-%M-%S)
BACKUP_PATH="$BACKUP_DIR/$DATE"

mkdir -p "$BACKUP_PATH"

echo "📦 Backup en cours vers $BACKUP_PATH ..."

# Backup SQLite (via le conteneur pour garantir l'intégrité)
echo "  → Base de données SQLite..."
docker run --rm \
  -v site_web_devblog_data:/data \
  -v "$(realpath "$BACKUP_PATH")":/backup \
  alpine sh -c "cp -r /data /backup/data"

# Backup uploads
echo "  → Fichiers uploadés (images/vidéos)..."
docker run --rm \
  -v site_web_devblog_uploads:/uploads \
  -v "$(realpath "$BACKUP_PATH")":/backup \
  alpine sh -c "cp -r /uploads /backup/uploads"

# Compression
echo "  → Compression..."
tar -czf "$BACKUP_DIR/backup_$DATE.tar.gz" -C "$BACKUP_DIR" "$DATE"
rm -rf "$BACKUP_PATH"

echo "✅ Backup terminé : $BACKUP_DIR/backup_$DATE.tar.gz"

# Nettoyage automatique : garder seulement les 10 derniers backups
cd "$BACKUP_DIR"
ls -t backup_*.tar.gz 2>/dev/null | tail -n +11 | xargs -r rm --
echo "🧹 Anciens backups nettoyés (10 gardés max)"
