import { useAuth } from '../context/AuthContext.jsx'
import AdminLogin from './AdminLogin.jsx'

/**
 * Protège les routes admin.
 * Affiche le login Steam si l'utilisateur n'est pas authentifié.
 */
export default function AdminGuard({ children }) {
  const { authenticated, loading } = useAuth()

  if (loading) {
    return (
      <div style={{
        minHeight: '100vh', display: 'flex', alignItems: 'center',
        justifyContent: 'center', background: '#0d0d0f', color: '#71717a',
        fontFamily: 'system-ui, sans-serif', fontSize: '0.9rem', gap: '12px',
      }}>
        <div style={{
          width: 20, height: 20, border: '2px solid #a78bfa',
          borderTopColor: 'transparent', borderRadius: '50%',
          animation: 'spin 0.7s linear infinite',
        }} />
        Vérification de l'authentification…
        <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
      </div>
    )
  }

  if (!authenticated) {
    return <AdminLogin />
  }

  return children
}
