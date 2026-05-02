// ============================================================
// DatabasePanel — browser read-only de la DB du jeu (PostgreSQL)
// ============================================================
// 2 colonnes : liste des tables a gauche, viewer a droite
// (paginated grid des rows). Pour edits / SQL custom -> Adminer.
// ============================================================

import { useEffect, useState } from 'react'
import { Database, RefreshCw, ChevronLeft, ChevronRight } from 'lucide-react'
import './DatabasePanel.css'

const PAGE_SIZE = 50

function formatCell(value) {
  if (value === null || value === undefined) return <span className="db-null">NULL</span>
  if (value === true) return 'true'
  if (value === false) return 'false'
  if (value instanceof Date) return value.toISOString().replace('T', ' ').slice(0, 19)
  if (typeof value === 'object') return JSON.stringify(value)
  const str = String(value)
  if (str.length > 200) return str.substring(0, 200) + '…'
  return str
}

export default function DatabasePanel() {
  const [tables, setTables]         = useState([])
  const [tablesError, setTablesError] = useState(null)
  const [loadingTables, setLoadingTables] = useState(true)

  const [selected, setSelected]     = useState(null)
  const [rows, setRows]             = useState([])
  const [columns, setColumns]       = useState([])
  const [total, setTotal]           = useState(0)
  const [offset, setOffset]         = useState(0)
  const [loadingRows, setLoadingRows] = useState(false)
  const [rowsError, setRowsError]   = useState(null)

  async function loadTables() {
    setLoadingTables(true)
    setTablesError(null)
    try {
      const r = await fetch('/api/db/tables', { credentials: 'include' })
      const data = await r.json()
      if (!r.ok) throw new Error(data.detail || data.error || `HTTP ${r.status}`)
      setTables(data.tables || [])
    } catch (e) {
      setTablesError(e.message)
    } finally {
      setLoadingTables(false)
    }
  }

  async function loadRows(tableName, newOffset = 0) {
    setLoadingRows(true)
    setRowsError(null)
    try {
      const r = await fetch(`/api/db/tables/${encodeURIComponent(tableName)}?limit=${PAGE_SIZE}&offset=${newOffset}`, {
        credentials: 'include',
      })
      const data = await r.json()
      if (!r.ok) throw new Error(data.detail || data.error || `HTTP ${r.status}`)
      setRows(data.rows || [])
      setTotal(data.total || 0)
      setOffset(newOffset)
      // Deduit les colonnes du 1er row si dispo
      if (data.rows && data.rows.length > 0) {
        setColumns(Object.keys(data.rows[0]))
      } else {
        // Sinon recupere le schema
        const sr = await fetch(`/api/db/tables/${encodeURIComponent(tableName)}/schema`, { credentials: 'include' })
        const sdata = await sr.json()
        if (sr.ok) setColumns((sdata.columns || []).map(c => c.column_name))
      }
    } catch (e) {
      setRowsError(e.message)
      setRows([])
      setColumns([])
    } finally {
      setLoadingRows(false)
    }
  }

  useEffect(() => { loadTables() }, [])

  function selectTable(name) {
    setSelected(name)
    loadRows(name, 0)
  }

  const totalPages = Math.ceil(total / PAGE_SIZE)
  const currentPage = Math.floor(offset / PAGE_SIZE) + 1

  return (
    <div className="db-page">
      <header className="db-header">
        <h1><Database size={20} style={{ verticalAlign: '-3px' }} /> Database — Jeu</h1>
        <p className="db-subtitle">
          Vue read-only du PostgreSQL du jeu (OpenFrameworkDb).
          Pour les edits ou le SQL custom, utilise Adminer (port 8080).
        </p>
        <button className="db-btn" onClick={loadTables} disabled={loadingTables} title="Refresh la liste des tables">
          <RefreshCw size={14} /> {loadingTables ? 'Chargement…' : 'Refresh'}
        </button>
      </header>

      {tablesError && (
        <div className="db-error">⚠ Connexion DB : {tablesError}</div>
      )}

      <div className="db-layout">
        {/* ── Liste des tables ── */}
        <aside className="db-tables">
          <div className="db-tables-title">
            Tables <span>({tables.length})</span>
          </div>
          <ul>
            {tables.map(t => (
              <li
                key={t.name}
                className={`db-table-item${selected === t.name ? ' db-table-item--active' : ''}`}
                onClick={() => selectTable(t.name)}
              >
                <span className="db-table-name">{t.name}</span>
                <span className="db-table-count">{t.rows ?? 0}</span>
              </li>
            ))}
            {!loadingTables && tables.length === 0 && !tablesError && (
              <li className="db-empty">Aucune table trouvee.</li>
            )}
          </ul>
        </aside>

        {/* ── Viewer ── */}
        <section className="db-viewer">
          {!selected ? (
            <div className="db-placeholder">
              <Database size={32} />
              <p>Selectionne une table a gauche pour voir son contenu.</p>
            </div>
          ) : rowsError ? (
            <div className="db-error">⚠ {rowsError}</div>
          ) : loadingRows ? (
            <p className="db-placeholder">Chargement…</p>
          ) : (
            <>
              <div className="db-viewer-header">
                <h2>{selected}</h2>
                <span className="db-viewer-count">
                  {total} ligne{total > 1 ? 's' : ''}
                </span>
                {totalPages > 1 && (
                  <div className="db-pager">
                    <button
                      className="db-btn"
                      disabled={offset === 0}
                      onClick={() => loadRows(selected, Math.max(0, offset - PAGE_SIZE))}
                    >
                      <ChevronLeft size={14} />
                    </button>
                    <span>{currentPage} / {totalPages}</span>
                    <button
                      className="db-btn"
                      disabled={offset + PAGE_SIZE >= total}
                      onClick={() => loadRows(selected, offset + PAGE_SIZE)}
                    >
                      <ChevronRight size={14} />
                    </button>
                  </div>
                )}
              </div>

              <div className="db-table-wrap">
                <table className="db-table">
                  <thead>
                    <tr>
                      {columns.map(c => <th key={c}>{c}</th>)}
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={columns.length || 1} className="db-empty-row">Table vide</td></tr>
                    ) : rows.map((row, i) => (
                      <tr key={i}>
                        {columns.map(c => <td key={c}>{formatCell(row[c])}</td>)}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </section>
      </div>
    </div>
  )
}
