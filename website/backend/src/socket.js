// ── Socket.io — gestionnaire temps réel ───────────────────────────────────
// Initialise le serveur Socket.io et expose broadcast() pour les routes.
// Gère aussi le streaming live de logs console (console:subscribe).

import { Server } from 'socket.io'
import passport from './auth.js'
import { findService } from './services.js'
import { streamContainerLogs } from './docker.js'

let io = null

// containerName → { stop: Function, room: string }
const activeStreams = new Map()

/**
 * Attache Socket.io au serveur HTTP Express.
 * @param {import('http').Server} httpServer
 * @param {string} frontendUrl
 * @param {Function} sessionMiddleware  middleware express-session à partager
 */
export function initSocket(httpServer, frontendUrl, sessionMiddleware) {
  io = new Server(httpServer, {
    cors: {
      origin: [frontendUrl, 'http://localhost:5173', 'http://localhost:3001'],
      credentials: true,
    },
  })

  // Applique session + passport aux handshakes Socket.io pour avoir socket.request.user
  const wrap = (fn) => (socket, next) => fn(socket.request, socket.request.res || {}, next)
  io.use(wrap(sessionMiddleware))
  io.use(wrap(passport.initialize()))
  io.use(wrap(passport.session()))

  io.on('connection', (socket) => {
    console.log(`🔌 Socket connecté : ${socket.id}`)

    // disconnecting = avant que les rooms soient vidées (contrairement à disconnect)
    socket.on('disconnecting', () => {
      for (const room of socket.rooms) {
        if (room.startsWith('console:')) {
          _leaveConsole(socket, room.replace('console:', ''))
        }
      }
    })

    socket.on('disconnect', () => {
      console.log(`🔌 Socket déconnecté : ${socket.id}`)
    })

    // ── Console streaming ───────────────────────────────────────────────
    socket.on('console:subscribe', (serviceId) => {
      const user = socket.request.user
      if (!user || user.role !== 'owner') {
        socket.emit('console:error', { serviceId, message: 'Accès refusé' })
        return
      }

      const svc = findService(serviceId)
      if (!svc) {
        socket.emit('console:error', { serviceId, message: 'Service inconnu' })
        return
      }

      const room = `console:${serviceId}`
      socket.join(room)

      // Lance le stream Docker si personne ne l'a déjà ouvert pour ce container
      if (!activeStreams.has(svc.container)) {
        const stop = streamContainerLogs(svc.container, {
          onLine:  (line) => io.to(room).emit('console:line', { serviceId, line }),
          onEnd:   ()    => {
            io.to(room).emit('console:end', { serviceId })
            activeStreams.delete(svc.container)
          },
          onError: (err) => {
            io.to(room).emit('console:error', { serviceId, message: err.message })
            activeStreams.delete(svc.container)
          },
        })
        activeStreams.set(svc.container, { stop, room })
      }
    })

    socket.on('console:unsubscribe', (serviceId) => {
      _leaveConsole(socket, serviceId)
    })
  })

  return io
}

function _leaveConsole(socket, serviceId) {
  const room = `console:${serviceId}`
  socket.leave(room)

  // Arrête le stream Docker si plus aucun client n'écoute cette room
  const remaining = io?.sockets.adapter.rooms.get(room)
  if (!remaining || remaining.size === 0) {
    const svc = findService(serviceId)
    if (svc) {
      const entry = activeStreams.get(svc.container)
      if (entry) {
        entry.stop()
        activeStreams.delete(svc.container)
      }
    }
  }
}

/**
 * Émet un événement à tous les clients connectés.
 * @param {string} event  Nom de l'événement (ex: 'hub_updated')
 * @param {object} data   Payload
 */
export function broadcast(event, data = {}) {
  if (io) io.emit(event, data)
}
