// ============================================================
// game-db.js — connection au SQL Server du jeu (OpenFrameworkDb)
// ============================================================
// Pool MSSQL partage. Connexion lazy : ouverte au 1er query, retry
// automatique en cas de deconnexion. Lecture seule cote routes
// (les ecritures passent par l'API .NET avec EF Core).
// ============================================================

import sql from 'mssql'

const config = {
  server:   process.env.GAME_DB_HOST     || 'sqlserver',
  port:     parseInt(process.env.GAME_DB_PORT || '1433', 10),
  database: process.env.GAME_DB_NAME     || 'OpenFrameworkDb',
  user:     process.env.GAME_DB_USER     || 'sa',
  password: process.env.GAME_DB_PASSWORD || 'OpenFwBootstrap_Pa55!',
  options: {
    trustServerCertificate: true,
    encrypt: false,
  },
  pool: {
    max: 5,
    min: 0,
    idleTimeoutMillis: 30000,
  },
  requestTimeout: 15000,
}

let pool = null
let connecting = null

export async function getPool() {
  if (pool && pool.connected) return pool
  if (connecting) return connecting
  connecting = (async () => {
    try {
      pool = new sql.ConnectionPool(config)
      pool.on('error', (err) => {
        console.error('[game-db] pool error:', err.message)
      })
      await pool.connect()
      return pool
    } finally {
      connecting = null
    }
  })()
  return connecting
}

/**
 * Execute une requete SELECT en lecture seule. Retourne les rows.
 * Throws si MSSQL est down ou la query echoue.
 */
export async function query(sqlText, params = {}) {
  const p = await getPool()
  const request = p.request()
  for (const [key, value] of Object.entries(params)) {
    request.input(key, value)
  }
  const result = await request.query(sqlText)
  return result.recordset
}
