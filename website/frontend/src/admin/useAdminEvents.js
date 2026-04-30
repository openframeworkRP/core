import { useEffect, useRef } from 'react'

/**
 * Écoute les événements SSE du serveur admin et appelle le callback associé.
 *
 * @param {string[]} events   Liste des noms d'événements à écouter
 *                            ex: ['hub_updated', 'members_updated']
 * @param {Function} callback Fonction appelée quand un des événements arrive
 *
 * Utilise une URL relative (/api/events) pour passer par le proxy Vite en dev
 * et éviter les problèmes CORS / cookies cross-origin en production.
 * Le callback est toujours la version la plus récente (pas de stale closure).
 */
export function useAdminEvents(events, callback) {
  // Garde toujours la référence fraîche du callback sans relancer l'effet
  const cbRef = useRef(callback)
  useEffect(() => { cbRef.current = callback })

  useEffect(() => {
    // URL relative → proxy Vite en dev, Cloudflare Tunnel en prod (pas de CORS)
    const es = new EventSource('/api/events', { withCredentials: true })

    const handler = () => cbRef.current()
    for (const event of events) {
      es.addEventListener(event, handler)
    }

    // Reconnexion silencieuse — EventSource le fait nativement
    es.onerror = () => {}

    return () => {
      for (const event of events) {
        es.removeEventListener(event, handler)
      }
      es.close()
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []) // volontairement stable — events ne change pas entre renders
}
