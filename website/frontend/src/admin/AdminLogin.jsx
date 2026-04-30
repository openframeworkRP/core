import { Wrench, Zap } from 'lucide-react'
import './AdminLogin.css'
import SEO from '../components/SEO.jsx'

const IS_DEV = import.meta.env.DEV
const API_BASE = import.meta.env.VITE_API_URL || ''

export default function AdminLogin() {
  const params = new URLSearchParams(window.location.search)
  const error = params.get('error')

  const handleSteamLogin = () => {
    window.location.href = `${API_BASE}/auth/steam`
  }

  const handleDevLogin = () => {
    window.location.href = `${API_BASE}/auth/dev-login`
  }

  return (
    <>
    <SEO title="Admin Login" noIndex />
    <div className="adm-login">
      <div className="adm-login__card">
        <div className="adm-login__logo">
          <Wrench size={48} />
        </div>
        <h1 className="adm-login__title">S&amp;Box Studio Admin</h1>
        <p className="adm-login__sub">
          Connecte-toi avec ton compte Steam pour accéder au panel d'administration.
        </p>

        <button className="adm-login__btn" onClick={handleSteamLogin}>
          <img
            src="https://store.cloudflare.steamstatic.com/public/shared/images/header/logo_steam.svg"
            alt="Steam"
          />
          Se connecter via Steam
        </button>

        {IS_DEV && (
          <button className="adm-login__btn adm-login__btn--dev" onClick={handleDevLogin}>
            <Zap size={16} />
            Dev Login (bypass Steam)
          </button>
        )}

        {error === 'unauthorized' && (
          <div className="adm-login__error">
            ⛔ Ton compte Steam n'est pas autorisé à accéder à l'administration.
          </div>
        )}

        <p className="adm-login__footer">Accès réservé aux administrateurs autorisés.</p>
      </div>
    </div>
    </>
  )
}
