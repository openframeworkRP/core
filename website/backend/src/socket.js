// ── Socket.io — gestionnaire temps réel ───────────────────────────────────
// Initialise le serveur Socket.io et expose emit() pour les routes.

import { Server } from 'socket.io'

let io = null

/**
 * Attache Socket.io au serveur HTTP Express.
 * @param {import('http').Server} httpServer
 * @param {string} frontendUrl
 */
export function initSocket(httpServer, frontendUrl) {
  io = new Server(httpServer, {
    cors: {
      origin: [frontendUrl, 'http://localhost:5173', 'http://localhost:3001'],
      credentials: true,
    },
  })

  io.on('connection', (socket) => {
    console.log(`🔌 Socket connecté : ${socket.id}`)
    socket.on('disconnect', () => {
      console.log(`🔌 Socket déconnecté : ${socket.id}`)
    })
  })

  return io
}

/**
 * Émet un événement à tous les clients connectés.
 * Remplace l'ancien broadcast() SSE.
 * @param {string} event  Nom de l'événement (ex: 'hub_updated')
 * @param {object} data   Payload (peut être vide {})
 */
export function broadcast(event, data = {}) {
  if (io) io.emit(event, data)
}
