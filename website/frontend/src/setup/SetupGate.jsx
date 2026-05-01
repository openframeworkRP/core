// ============================================================
// SetupGate — wrapper qui decide si on affiche le site normal
// ou le wizard d'installation au premier lancement.
// ============================================================
// Au mount : fetch /api/setup/status. Si needsSetup, on render
// SetupWizard a la place des children. Sinon, render normal.
// ============================================================

import { useEffect, useState } from 'react'
import SetupWizard from './SetupWizard.jsx'

export default function SetupGate({ children }) {
  const [status, setStatus] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    let cancelled = false
    fetch('/api/setup/status')
      .then(r => r.json())
      .then(data => { if (!cancelled) setStatus(data) })
      .catch(e => { if (!cancelled) setError(e.message) })
    return () => { cancelled = true }
  }, [])

  // Si l'API website est down, on n'a pas le moyen de savoir si setup est
  // necessaire — on tombe gracieusement sur le site normal.
  if (error) return children

  // Pendant le fetch initial, ne rien render (evite un flash de site puis wizard)
  if (status === null) return null

  if (status.needsSetup) {
    return <SetupWizard onComplete={() => setStatus({ ...status, needsSetup: false })} />
  }

  return children
}
