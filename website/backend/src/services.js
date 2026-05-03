// Liste partagée des services Docker exposés au Control Center.
// Importée par control.js et socket.js.

export const SERVICES = [
  { id: 'core.api',         container: 'core-api',              label: 'API du jeu (.NET 10)',    critical: true },
  { id: 'postgres',         container: 'core-postgres',         label: 'PostgreSQL (DB du jeu)',  critical: true },
  { id: 'redis',            container: 'core-redis',            label: 'Redis (cache)',           critical: true },
  { id: 'adminer',          container: 'core-adminer',          label: 'Adminer (UI DB)' },
  { id: 'website.api',      container: 'core-website-api',      label: 'API du website (Node)',   self: true },
  { id: 'website.frontend', container: 'core-website-frontend', label: 'Frontend (Vite)' },
]

export function findService(id) {
  return SERVICES.find(s => s.id === id)
}
