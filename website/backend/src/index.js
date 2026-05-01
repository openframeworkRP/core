import express from 'express'
import cors from 'cors'
import session from 'express-session'
import connectSqlite3 from 'connect-sqlite3'
import { createServer } from 'http'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'

import passport, { requireAuth, requireRole } from './auth.js'
import { initSocket } from './socket.js'
import { initPermissions } from './permissions.js'
import authRouter          from './routes/auth.js'
import gamesRouter         from './routes/games.js'
import postsRouter         from './routes/posts.js'
import blocksRouter        from './routes/blocks.js'
import uploadRouter        from './routes/upload.js'
import translateRouter     from './routes/translate.js'
import jobsRouter          from './routes/jobs.js'
import usersRouter         from './routes/users.js'
import bugsRouter          from './routes/bugs.js'
import statsRouter         from './routes/stats.js'
import membersRouter       from './routes/members.js'
import ogRouter            from './routes/og.js'
import devblogArchiveRouter from './routes/devblog_archive.js'
import hubRouter            from './routes/hub.js'
import fabRouter            from './routes/fab.js'
import videosRouter         from './routes/videos.js'
import imagesRouter         from './routes/images.js'
import rulesRouter          from './routes/rules.js'
import wikiRouter           from './routes/wiki.js'
import docsRouter           from './routes/docs.js'
import nextcloudRouter      from './routes/nextcloud.js'
import assetsRouter         from './routes/assets.js'
import gameAdminRouter      from './routes/gameadmin.js'
import tokensRouter         from './routes/tokens.js'
import permissionsRouter    from './routes/permissions.js'
import setupRouter          from './routes/setup.js'
import controlRouter        from './routes/control.js'
import brandingRouter       from './routes/branding.js'
import roadmapRouter        from './routes/roadmap.js'

// Init des tables roles/pages/role_permissions + seed initial.
// Doit tourner avant que les routers ne soient utilisés.
initPermissions()

const __dirname = dirname(fileURLToPath(import.meta.url))
const PORT = process.env.PORT || 3001
const FRONTEND_URL = process.env.FRONTEND_URL || 'http://localhost:5173'
const IS_DEV = !process.env.NODE_ENV || process.env.NODE_ENV === 'development'

const SQLiteStore = connectSqlite3(session)

const app = express()
const httpServer = createServer(app)

// Initialise Socket.io sur le même serveur HTTP
initSocket(httpServer, FRONTEND_URL)

// Faire confiance au reverse proxy (Cloudflare Tunnel, nginx…)
// nécessaire pour que req.secure = true et que les cookies secure fonctionnent
app.set('trust proxy', 1)

app.use(cors({
  origin: [FRONTEND_URL, 'http://localhost:3001', 'http://localhost:5173'],
  credentials: true,
  methods: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS'],
  allowedHeaders: ['Content-Type', 'Authorization'],
}))
app.use(express.json({ limit: '10mb' }))

// ── Sessions (stockées dans SQLite) ───────────────────────────────────────
app.use(session({
  store: new SQLiteStore({ db: 'sessions.db', dir: join(__dirname, '../data') }),
  secret: process.env.SESSION_SECRET || 'change-this-secret-in-production',
  resave: false,
  saveUninitialized: false,
  cookie: {
    // En prod, Express fait confiance à X-Forwarded-Proto (trust proxy = 1)
    // 'auto' : secure si la requête originale était HTTPS, sinon false
    secure: IS_DEV ? false : 'auto',
    httpOnly: true,
    maxAge: 7 * 24 * 60 * 60 * 1000, // 7 jours
    sameSite: 'lax',
  },
}))

// ── Passport ───────────────────────────────────────────────────────────────
app.use(passport.initialize())
app.use(passport.session())

// ── Fichiers uploadés (public) ─────────────────────────────────────────────
app.use('/uploads', express.static(join(__dirname, '../uploads')))

// ── Routes auth Steam ──────────────────────────────────────────────────────
app.use('/auth', authRouter)

// ── Wizard de setup (PUBLIC : aucun user n'existe encore au 1er run) ─────
// Doit etre monte AVANT les autres routes pour que /api/setup/status
// ne soit pas intercepte par d'eventuels middlewares.
app.use('/api/setup', setupRouter)

// ── API (requireAuth appliqué par méthode dans chaque router) ─────────────
app.get('/api/health', (_req, res) => res.json({ ok: true, time: new Date() }))
app.use('/api/games',  gamesRouter)
app.use('/api/posts/:postId/blocks', (req, _res, next) => { next() }, blocksRouter)
app.use('/api/posts',              devblogArchiveRouter)  // export/import archive .devblog
app.use('/api/posts',              postsRouter)
app.use('/api/upload',     requireAuth, uploadRouter)
app.use('/api/translate',  requireAuth, translateRouter)
app.use('/api/jobs',       jobsRouter)
app.use('/api/users',      usersRouter)
app.use('/api/bugs',       bugsRouter)
app.use('/api/stats',      statsRouter)
app.use('/api/members',    membersRouter)
app.use('/og',             ogRouter)
app.use('/api/hub',        hubRouter)
app.use('/api/fab',        fabRouter)
app.use('/api/videos',     videosRouter)
app.use('/api/images',     imagesRouter)
app.use('/api/rules',      rulesRouter)
app.use('/api/wiki',       wikiRouter)
app.use('/api/docs',       docsRouter)
app.use('/api/nextcloud',  nextcloudRouter)
app.use('/api/assets',     assetsRouter)
app.use('/api/gameadmin',  gameAdminRouter)
app.use('/api/tokens',     tokensRouter)
app.use('/api/permissions', permissionsRouter)
app.use('/api/control',     controlRouter)
app.use('/api/branding',    brandingRouter)
app.use('/api/roadmap',     roadmapRouter)

httpServer.listen(PORT, () => {
  console.log(`✅  DevBlog API running on http://localhost:${PORT}`)
})
