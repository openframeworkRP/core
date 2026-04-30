import { createContext, useContext, useState, useEffect, useCallback } from 'react'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [user, setUser]            = useState(null)
  const [permissions, setPerms]    = useState({})
  const [loading, setLoading]      = useState(true)
  const [authenticated, setAuth]   = useState(false)

  const AUTH_BASE = import.meta.env.VITE_API_URL || ''

  const refresh = useCallback(async () => {
    try {
      const r = await fetch(`${AUTH_BASE}/auth/me`, { credentials: 'include' })
      const data = await r.json()
      setAuth(!!data.authenticated)
      setUser(data.user ? { ...data.user, member: data.member || null } : null)
      setPerms(data.permissions || {})
    } catch {
      setAuth(false)
      setUser(null)
      setPerms({})
    }
  }, [AUTH_BASE])

  useEffect(() => {
    refresh().finally(() => setLoading(false))
  }, [refresh])

  // Owner = passe-partout, sinon lit la matrice
  const can = useCallback((pageKey, action = 'view') => {
    if (!user) return false
    if (user.role === 'owner') return true
    return !!permissions?.[pageKey]?.[action]
  }, [user, permissions])

  const logout = async () => {
    await fetch(`${AUTH_BASE}/auth/logout`, { method: 'POST', credentials: 'include' })
    setAuth(false)
    setUser(null)
    setPerms({})
  }

  return (
    <AuthContext.Provider value={{ authenticated, user, permissions, can, loading, logout, refresh }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  return useContext(AuthContext)
}
