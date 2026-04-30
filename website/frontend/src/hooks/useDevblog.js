// Hook pour charger les posts et les jeux depuis l'API
import { useState, useEffect } from 'react'

const API = import.meta.env.VITE_API_URL || ''

export function usePosts() {
  const [posts,   setPosts]   = useState([])
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState(null)

  useEffect(() => {
    fetch(`${API}/api/posts`)
      .then(r => r.json())
      .then(setPosts)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false))
  }, [])

  return { posts, loading, error }
}

export function usePost(slug) {
  const [post,    setPost]    = useState(null)
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState(null)

  useEffect(() => {
    if (!slug) return
    setPost(null)
    setError(null)
    setLoading(true)
    fetch(`${API}/api/posts/${slug}`)
      .then(r => { if (!r.ok) throw new Error('Not found'); return r.json() })
      .then(setPost)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false))
  }, [slug])

  return { post, loading, error }
}

export function useGames() {
  const [games,   setGames]   = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch(`${API}/api/games`)
      .then(r => r.json())
      .then(data => {
        // Ajoute l'entrée "Tout voir" en tête
        setGames([
          { id: 0, slug: 'all', label_fr: 'Tout voir', label_en: 'All', color: null },
          ...data,
        ])
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  return { games, loading }
}
