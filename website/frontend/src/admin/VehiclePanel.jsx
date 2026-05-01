import { ExternalLink } from 'lucide-react'

const ARTIFACT_URL = 'https://claude.site/public/artifacts/8ac61bc9-46d2-4f67-aabc-7d7ec6cca7a0/embed'

export default function VehiclePanel() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', minHeight: 'calc(100vh - 140px)', background: '#161a26' }}>
      {/* Toolbar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '8px 12px', background: '#242424', borderBottom: '1px solid #333', flexShrink: 0,
      }}>
        <span style={{ fontWeight: 600, color: '#e8eaed', fontSize: 14, flex: 1 }}>Vehicle Prefab Generator</span>
        <a href={ARTIFACT_URL.replace('/embed', '')} target="_blank" rel="noreferrer"
          style={{ display: 'flex', alignItems: 'center', gap: 4, color: '#888', fontSize: 12, textDecoration: 'none' }}
          title="Ouvrir dans un nouvel onglet">
          <ExternalLink size={14} /> Ouvrir
        </a>
      </div>

      {/* Iframe */}
      <iframe
        src={ARTIFACT_URL}
        title="Vehicle Prefab Generator"
        style={{ flex: 1, border: 'none', width: '100%' }}
        allow="clipboard-write"
        allowFullScreen
      />
    </div>
  )
}
