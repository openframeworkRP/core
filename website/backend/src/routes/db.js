// ============================================================
// /api/db/* — browser read-only de la DB du jeu (SQL Server)
// ============================================================
// GET /tables          : liste des tables avec row count
// GET /tables/:name/schema : metadata des colonnes
// GET /tables/:name?limit=50&offset=0 : rows paginees
// ============================================================
// Owner only (acces direct a la DB = sensible). Read-only — aucune
// route POST/PUT/DELETE, on ne supporte pas le SQL custom non plus
// (eviter SQL injection / data leak via la console admin).
// ============================================================

import { Router } from 'express'
import { requireAuth, requireRole } from '../auth.js'
import { query } from '../game-db.js'

const router = Router()

// Tout le router est owner-only
router.use(requireAuth, requireRole('owner'))

// Helper : valide qu'un nom de table est safe (alphanumerique + _).
// On l'utilise dans des string templates (impossible de bind un
// table name comme parametre en MSSQL).
function safeTableName(name) {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(name || '') ? name : null
}

// ── GET /api/db/tables ─────────────────────────────────────────────────
router.get('/tables', async (_req, res) => {
  try {
    const rows = await query(`
      SELECT
        t.name AS table_name,
        s.name AS schema_name,
        SUM(p.rows) AS row_count
      FROM sys.tables t
      INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
      INNER JOIN sys.partitions p ON p.object_id = t.object_id
      WHERE p.index_id IN (0, 1)
        AND t.is_ms_shipped = 0
      GROUP BY t.name, s.name
      ORDER BY t.name
    `)
    res.json({ tables: rows.map(r => ({
      name:   r.table_name,
      schema: r.schema_name,
      rows:   r.row_count,
    })) })
  } catch (e) {
    res.status(500).json({ error: 'db-unreachable', detail: e.message })
  }
})

// ── GET /api/db/tables/:name/schema ────────────────────────────────────
router.get('/tables/:name/schema', async (req, res) => {
  const name = safeTableName(req.params.name)
  if (!name) return res.status(400).json({ error: 'invalid-table-name' })
  try {
    const cols = await query(`
      SELECT
        c.name           AS column_name,
        t.name           AS data_type,
        c.max_length     AS max_length,
        c.is_nullable    AS is_nullable,
        c.is_identity    AS is_identity
      FROM sys.columns c
      INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
      WHERE c.object_id = OBJECT_ID(@table_name)
      ORDER BY c.column_id
    `, { table_name: name })
    res.json({ columns: cols })
  } catch (e) {
    res.status(500).json({ error: 'schema-failed', detail: e.message })
  }
})

// ── GET /api/db/tables/:name?limit=50&offset=0 ─────────────────────────
router.get('/tables/:name', async (req, res) => {
  const name = safeTableName(req.params.name)
  if (!name) return res.status(400).json({ error: 'invalid-table-name' })

  const limit  = Math.min(Math.max(parseInt(req.query.limit, 10)  || 50, 1), 500)
  const offset = Math.max(parseInt(req.query.offset, 10) || 0, 0)
  const orderBy = safeTableName(req.query.order_by || '') || null

  // OFFSET/FETCH MSSQL exige un ORDER BY. Si pas de colonne fournie,
  // on prend la premiere colonne de la table par defaut.
  let orderClause = orderBy ? `[${orderBy}]` : '1'
  if (!orderBy) {
    try {
      const cols = await query(`
        SELECT TOP 1 c.name
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(@t)
        ORDER BY c.column_id
      `, { t: name })
      if (cols[0]?.name) orderClause = `[${cols[0].name}]`
    } catch { /* fallback sur '1' */ }
  }

  try {
    const rows = await query(`
      SELECT * FROM [${name}]
      ORDER BY ${orderClause}
      OFFSET ${offset} ROWS
      FETCH NEXT ${limit} ROWS ONLY
    `)
    const totalRow = await query(`SELECT COUNT(*) AS total FROM [${name}]`)
    res.json({
      rows,
      total:  totalRow[0]?.total || 0,
      limit,
      offset,
    })
  } catch (e) {
    res.status(500).json({ error: 'query-failed', detail: e.message })
  }
})

export default router
