// ============================================================
// /api/db/* — browser read-only de la DB du jeu (PostgreSQL)
// ============================================================
// GET /tables                  : liste des tables avec row count
// GET /tables/:name/schema     : metadata des colonnes
// GET /tables/:name?limit=50&offset=0 : rows paginees
// ============================================================
// Owner only. Read-only — pas de POST/PUT/DELETE ni SQL custom.
// ============================================================

import { Router } from 'express'
import { requireAuth, requireRole } from '../auth.js'
import { query } from '../game-db.js'

const router = Router()

router.use(requireAuth, requireRole('owner'))

// Helper : valide qu'un nom de table est safe (alphanumerique + _).
function safeTableName(name) {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(name || '') ? name : null
}

// ── GET /api/db/tables ─────────────────────────────────────────────────
router.get('/tables', async (_req, res) => {
  try {
    const rows = await query(`
      SELECT
        relname        AS table_name,
        schemaname     AS schema_name,
        n_live_tup     AS row_count
      FROM pg_stat_user_tables
      WHERE schemaname = 'public'
      ORDER BY relname
    `)
    res.json({ tables: rows.map(r => ({
      name:   r.table_name,
      schema: r.schema_name,
      rows:   Number(r.row_count),
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
        column_name,
        data_type,
        character_maximum_length  AS max_length,
        is_nullable,
        column_default            AS is_identity
      FROM information_schema.columns
      WHERE table_schema = 'public'
        AND table_name   = $1
      ORDER BY ordinal_position
    `, [name])
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

  // Si pas de colonne fournie, on prend la 1ere de la table.
  let orderClause = orderBy ? `"${orderBy}"` : '1'
  if (!orderBy) {
    try {
      const cols = await query(`
        SELECT column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name   = $1
        ORDER BY ordinal_position
        LIMIT 1
      `, [name])
      if (cols[0]?.column_name) orderClause = `"${cols[0].column_name}"`
    } catch { /* fallback sur 1 */ }
  }

  try {
    const rows = await query(
      `SELECT * FROM "${name}" ORDER BY ${orderClause} LIMIT $1 OFFSET $2`,
      [limit, offset],
    )
    const totalRow = await query(`SELECT COUNT(*)::int AS total FROM "${name}"`)
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
