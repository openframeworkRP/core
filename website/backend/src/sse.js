// ── Server-Sent Events — gestionnaire de clients ──────────────────────────
// Chaque client admin connecté reçoit les événements en temps réel.
// Les routes qui mutent des données appellent broadcast() après la réponse.

const clients = new Set()

/** Enregistre une connexion SSE. */
export function addClient(res) {
  clients.add(res)
}

/** Supprime une connexion SSE (déconnexion / timeout). */
export function removeClient(res) {
  clients.delete(res)
}

/**
 * Envoie un événement SSE à tous les clients connectés.
 * @param {string} event  Nom de l'événement (ex: 'hub_updated', 'games_updated')
 * @param {object} data   Payload JSON (peut être vide {})
 */
export function broadcast(event, data = {}) {
  const msg = `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`
  for (const res of clients) {
    try {
      res.write(msg)
    } catch {
      clients.delete(res)
    }
  }
}
