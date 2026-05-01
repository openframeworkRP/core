// ============================================================
// SetupWizard — 4 etapes pour configurer OpenFramework Core
// ============================================================
// 1. Bienvenue
// 2. Secrets (auto-generes via crypto.getRandomValues)
// 3. Steam (paste API key + admin SteamID)
// 4. Application (POST /api/setup/apply, polling, redirect)
// ============================================================

import { useState } from 'react'
import './setup.css'

// ── Generation de secrets cryptographiquement surs (cote browser) ──────
function genHexSecret(bytes) {
  const buf = new Uint8Array(bytes)
  crypto.getRandomValues(buf)
  return Array.from(buf).map(b => b.toString(16).padStart(2, '0')).join('')
}

function genBase64Secret(bytes) {
  const buf = new Uint8Array(bytes)
  crypto.getRandomValues(buf)
  let bin = ''
  for (const b of buf) bin += String.fromCharCode(b)
  // base64url-ish (sans padding pour eviter les soucis dans le .env)
  return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function maskSecret(s) {
  if (!s) return ''
  if (s.length <= 16) return s
  return `${s.substring(0, 12)}…${s.substring(s.length - 6)}`
}

export default function SetupWizard() {
  const [step, setStep] = useState(0)

  // Secrets pre-generes (le user peut les regenerer)
  const [secrets, setSecrets] = useState(() => ({
    jwtKey:        genBase64Secret(64),
    serverSecret:  genHexSecret(32),
    sessionSecret: genHexSecret(32),
  }))

  // Champs Steam
  const [steamApiKey, setSteamApiKey] = useState('')
  const [allowedSteamIds, setAllowedSteamIds] = useState('')

  // Champs Branding (optionnels)
  const [siteName,      setSiteName]      = useState('')
  const [primaryColor,  setPrimaryColor]  = useState('#e07b39')
  const [accentColor,   setAccentColor]   = useState('#ffa726')
  const [logoUrl,       setLogoUrl]       = useState('')

  // Etat de l'application
  const [applying, setApplying]       = useState(false)
  const [applyResult, setApplyResult] = useState(null)
  const [applyError, setApplyError]   = useState(null)

  function regenerateSecrets() {
    setSecrets({
      jwtKey:        genBase64Secret(64),
      serverSecret:  genHexSecret(32),
      sessionSecret: genHexSecret(32),
    })
  }

  async function applyConfig() {
    setApplying(true)
    setApplyError(null)
    try {
      const branding = {}
      if (siteName.trim())      branding.site_name      = siteName.trim()
      if (siteName.trim())      branding.site_short_name = siteName.trim()
      if (primaryColor)         branding.primary_color  = primaryColor
      if (accentColor)          branding.accent_color   = accentColor
      if (logoUrl.trim())       branding.logo_url       = logoUrl.trim()

      const res = await fetch('/api/setup/apply', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          jwtKey:          secrets.jwtKey,
          serverSecret:    secrets.serverSecret,
          sessionSecret:   secrets.sessionSecret,
          steamApiKey:     steamApiKey.trim(),
          allowedSteamIds: allowedSteamIds.trim(),
          branding:        Object.keys(branding).length > 0 ? branding : null,
        }),
      })
      const data = await res.json()
      if (!res.ok) {
        setApplyError(data.error || `apply-failed (HTTP ${res.status})`)
        setApplying(false)
        return
      }
      setApplyResult(data)
      setApplying(false)
      // Auto-redirection vers /admin apres 20 secondes (le temps que les
      // services website soient recrees — environ 10-15s).
      setTimeout(() => { window.location.href = '/admin' }, 20000)
    } catch (e) {
      setApplyError(e.message)
      setApplying(false)
    }
  }

  // ── Definition des etapes ─────────────────────────────────────────────
  const steps = [
    {
      title: 'Bienvenue',
      content: (
        <>
          <h2>Configuration d'OpenFramework Core</h2>
          <p>
            Ce wizard te guide en <strong>3 etapes</strong> pour finaliser
            l'installation de ton serveur :
          </p>
          <ol>
            <li>
              <strong>Secrets</strong> — generation automatique de la JWT key, du
              server secret et du session secret (genere localement dans ton browser
              via <code>crypto.getRandomValues</code>).
            </li>
            <li>
              <strong>Steam</strong> — colle ta cle API Steam et ton SteamID64 admin.
            </li>
            <li>
              <strong>Application</strong> — ecriture du <code>.env</code>, restart de
              l'API .NET, attente du healthcheck, redirection vers le panel admin.
            </li>
          </ol>
          <p>
            Tu peux relancer ce wizard en supprimant{' '}
            <code>data/config/setup-complete.flag</code>.
          </p>
        </>
      ),
    },
    {
      title: 'Secrets',
      content: (
        <>
          <h2>Secrets generes automatiquement</h2>
          <p>
            Les valeurs sont generees dans ton browser et ne quittent jamais ton
            host (a part vers ton propre <code>.env</code>). Tu peux les regenerer
            autant de fois que tu veux avant de valider.
          </p>
          <div className="secret-row">
            <label>JWT Key</label>
            <code className="secret-value">{maskSecret(secrets.jwtKey)}</code>
          </div>
          <div className="secret-row">
            <label>Server Secret</label>
            <code className="secret-value">{maskSecret(secrets.serverSecret)}</code>
          </div>
          <div className="secret-row">
            <label>Session Secret</label>
            <code className="secret-value">{maskSecret(secrets.sessionSecret)}</code>
          </div>
          <button type="button" className="setup-btn-secondary" onClick={regenerateSecrets}>
            ↻ Regenerer
          </button>
        </>
      ),
    },
    {
      title: 'Steam',
      content: (
        <>
          <h2>Authentification Steam</h2>
          <div className="setup-field">
            <label htmlFor="steam-api-key">Cle API Steam</label>
            <p className="setup-hint">
              Obtiens-la sur{' '}
              <a href="https://steamcommunity.com/dev/apikey" target="_blank" rel="noopener noreferrer">
                steamcommunity.com/dev/apikey
              </a>
              . Champ optionnel — laisse vide si tu veux configurer plus tard.
            </p>
            <input
              id="steam-api-key"
              type="text"
              value={steamApiKey}
              onChange={e => setSteamApiKey(e.target.value)}
              placeholder="ex: 1234ABCD5678EF90..."
              autoComplete="off"
              spellCheck="false"
            />
          </div>
          <div className="setup-field">
            <label htmlFor="admin-steamid">Ton SteamID64 (admin du panel)</label>
            <p className="setup-hint">
              Trouve-le sur{' '}
              <a href="https://steamid.io" target="_blank" rel="noopener noreferrer">steamid.io</a>
              . Plusieurs admins : separe par virgule.
            </p>
            <input
              id="admin-steamid"
              type="text"
              value={allowedSteamIds}
              onChange={e => setAllowedSteamIds(e.target.value)}
              placeholder="ex: 76561198xxxxxxxxx"
              autoComplete="off"
              spellCheck="false"
            />
          </div>
        </>
      ),
    },
    {
      title: 'Personnalisation',
      content: (
        <>
          <h2>Personnalisation (optionnelle)</h2>
          <p>
            Configure le nom et le theme de ton instance. Tout est modifiable
            plus tard via <code>/admin/panel/branding</code>.
          </p>

          <div className="setup-field">
            <label htmlFor="brand-site-name">Nom du site</label>
            <p className="setup-hint">
              Affiche dans le header, le title du browser et le SEO. Laisse vide pour 'OpenFramework' par defaut.
            </p>
            <input
              id="brand-site-name"
              type="text"
              value={siteName}
              onChange={e => setSiteName(e.target.value)}
              placeholder="ex: Mon Serveur RP"
              autoComplete="off"
            />
          </div>

          <div className="setup-field">
            <label htmlFor="brand-primary">Couleur principale</label>
            <p className="setup-hint">Boutons, liens, accents. Format hex (#RRGGBB).</p>
            <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
              <input
                id="brand-primary"
                type="color"
                value={primaryColor}
                onChange={e => setPrimaryColor(e.target.value)}
                style={{ width: 60, height: 38, padding: 0, background: 'transparent', border: '1px solid #2a2f3e', borderRadius: 6, cursor: 'pointer' }}
              />
              <input
                type="text"
                value={primaryColor}
                onChange={e => setPrimaryColor(e.target.value)}
                style={{ flex: 1 }}
              />
            </div>
          </div>

          <div className="setup-field">
            <label htmlFor="brand-accent">Couleur accent</label>
            <p className="setup-hint">Hover, highlights, badges.</p>
            <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
              <input
                id="brand-accent"
                type="color"
                value={accentColor}
                onChange={e => setAccentColor(e.target.value)}
                style={{ width: 60, height: 38, padding: 0, background: 'transparent', border: '1px solid #2a2f3e', borderRadius: 6, cursor: 'pointer' }}
              />
              <input
                type="text"
                value={accentColor}
                onChange={e => setAccentColor(e.target.value)}
                style={{ flex: 1 }}
              />
            </div>
          </div>

          <div className="setup-field">
            <label htmlFor="brand-logo">URL du logo (optionnel)</label>
            <p className="setup-hint">URL absolue (https://...) ou relative (/img/logo.png). Laisse vide si pas de logo.</p>
            <input
              id="brand-logo"
              type="text"
              value={logoUrl}
              onChange={e => setLogoUrl(e.target.value)}
              placeholder="https://exemple.com/logo.png"
              autoComplete="off"
            />
          </div>
        </>
      ),
    },
    {
      title: 'Application',
      content: applying ? (
        <div className="setup-applying">
          <div className="setup-spinner" />
          <p>Ecriture du .env, restart de l'API .NET, attente du healthcheck…</p>
          <p className="setup-hint">Ca peut prendre jusqu'a 60 secondes.</p>
        </div>
      ) : applyResult ? (
        <div className="setup-success">
          <h2>✓ Configuration appliquee</h2>
          <p>
            L'API du jeu{' '}
            {applyResult.apiReady
              ? <strong style={{ color: '#4caf50' }}>repond correctement</strong>
              : <strong style={{ color: '#ffa726' }}>n'a pas (encore) repondu apres 60s</strong>}.
          </p>
          {applyResult.postSetupHint && (
            <div className="setup-error" style={{ background: '#1d2a1d', borderColor: '#3a5a3a', color: '#a5d6a7' }}>
              <strong>⏳ Reconnexion en cours</strong>
              <p style={{ margin: '8px 0 0' }}>{applyResult.postSetupHint}</p>
            </div>
          )}
          <p style={{ marginTop: 24 }}>
            <span className="setup-spinner" style={{ width: 16, height: 16, display: 'inline-block', verticalAlign: 'middle', marginRight: 8 }} />
            Patiente que les services soient prets — redirection vers le panel admin dans 20 secondes…
          </p>
          <a href="/admin" className="setup-btn" style={{ marginTop: 12 }}>
            J'attends pas, va sur /admin maintenant →
          </a>
        </div>
      ) : (
        <>
          <h2>Pret a appliquer</h2>
          <p>Le wizard va :</p>
          <ul>
            <li>Ecrire <code>.env</code> a la racine du repo (mode 0600)</li>
            <li>Restart le container <code>core-api</code> via Docker socket</li>
            <li>Attendre que <code>/health/ready</code> reponde 200 (max 60s)</li>
            <li>Poser <code>data/config/setup-complete.flag</code></li>
          </ul>
          {applyError && (
            <div className="setup-error">
              ⚠ Erreur : <code>{applyError}</code>
            </div>
          )}
          <button type="button" className="setup-btn" onClick={applyConfig}>
            Appliquer la configuration
          </button>
        </>
      ),
    },
  ]

  const current = steps[step]
  const isLast  = step === steps.length - 1
  const canPrev = step > 0 && !applying && !applyResult
  const canNext = !isLast && !applying && !applyResult

  return (
    <div className="setup-overlay">
      <div className="setup-modal">
        <header>
          <h1>OpenFramework <span>Setup</span></h1>
          <div className="setup-progress">
            {steps.map((s, i) => (
              <div
                key={i}
                className={`setup-progress-step${i === step ? ' active' : ''}${i < step ? ' done' : ''}`}
              >
                {i + 1}. {s.title}
              </div>
            ))}
          </div>
        </header>

        <main>{current.content}</main>

        <footer>
          {canPrev && (
            <button type="button" className="setup-btn-secondary" onClick={() => setStep(s => s - 1)}>
              ← Precedent
            </button>
          )}
          <div style={{ flex: 1 }} />
          {canNext && (
            <button type="button" className="setup-btn" onClick={() => setStep(s => s + 1)}>
              Suivant →
            </button>
          )}
        </footer>
      </div>
    </div>
  )
}
