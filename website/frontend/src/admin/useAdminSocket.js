import { useEffect, useRef } from 'react'
import { io } from 'socket.io-client'

// Singleton — une seule connexion WebSocket par onglet
let socket = null

export function getSocket() {
  if (!socket) {
    socket = io({
      path: '/socket.io',
      withCredentials: true,
      transports: ['websocket', 'polling'],
    })
    socket.on('connect', () => console.log('[socket] connecté :', socket.id))
    socket.on('disconnect', () => console.log('[socket] déconnecté'))
    socket.on('connect_error', (e) => console.warn('[socket] erreur :', e.message))
  }
  return socket
}

/**
 * Écoute des événements Socket.io et appelle callback(eventName, data) à chaque réception.
 *
 * @param {string[]} events   ex: ['task_updated', 'task_deleted']
 * @param {Function} callback Appelé avec (eventName, data) quand un des événements arrive
 */
export function useAdminSocket(events, callback) {
  const cbRef = useRef(callback)
  useEffect(() => { cbRef.current = callback })

  useEffect(() => {
    const s = getSocket()
    const handlers = {}

    for (const event of events) {
      const handler = (data) => cbRef.current(event, data)
      handlers[event] = handler
      s.on(event, handler)
    }

    return () => {
      for (const event of events) {
        s.off(event, handlers[event])
      }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []) // stable — events ne change pas entre renders
}
