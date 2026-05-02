// ============================================================
// game-db.js — connexion PostgreSQL du jeu (OpenFrameworkDb)
// ============================================================
// Pool pg partage. Les ecritures passent par l'API .NET (EF Core),
// ce module est read-only cote website.
// ============================================================

import pg from 'pg'

const { Pool } = pg

const pool = new Pool({
  host:               process.env.GAME_DB_HOST     || 'postgres',
  port:               parseInt(process.env.GAME_DB_PORT || '5432', 10),
  database:           process.env.GAME_DB_NAME     || 'OpenFrameworkDb',
  user:               process.env.GAME_DB_USER     || 'postgres',
  password:           process.env.GAME_DB_PASSWORD || 'OpenFwBootstrap_Pa55!',
  max:                5,
  idleTimeoutMillis:  30000,
  connectionTimeoutMillis: 15000,
})

pool.on('error', (err) => console.error('[game-db] pool error:', err.message))

/**
 * Execute une requete SELECT en lecture seule. Retourne les rows.
 * params est un tableau positionnel : $1, $2, ...
 * Throws si PostgreSQL est down ou la query echoue.
 */
export async function query(sqlText, params = []) {
  const result = await pool.query(sqlText, params)
  return result.rows
}
