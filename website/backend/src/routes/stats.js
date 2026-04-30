import express from 'express'
import db from '../db.js'
import { requireAuth } from '../auth.js'

const router = express.Router()

// GET /api/stats  — tableau de bord admin
router.get('/', requireAuth, (req, res) => {
  // ── Totaux ────────────────────────────────────────────────────────────────
  const totalPosts     = db.prepare('SELECT COUNT(*) AS c FROM posts').get().c
  const publishedPosts = db.prepare('SELECT COUNT(*) AS c FROM posts WHERE published = 1').get().c
  const draftPosts     = db.prepare('SELECT COUNT(*) AS c FROM posts WHERE published = 0').get().c
  const totalGames     = db.prepare('SELECT COUNT(*) AS c FROM games').get().c
  const totalUsers     = db.prepare('SELECT COUNT(*) AS c FROM users').get().c
  const openJobs       = db.prepare('SELECT COUNT(*) AS c FROM jobs WHERE is_open = 1').get().c
  const totalJobs      = db.prepare('SELECT COUNT(*) AS c FROM jobs').get().c
  const totalBugs      = db.prepare('SELECT COUNT(*) AS c FROM bug_reports').get().c
  const pendingBugs    = db.prepare("SELECT COUNT(*) AS c FROM bug_reports WHERE status = 'pending'").get().c
  const patchedBugs    = db.prepare("SELECT COUNT(*) AS c FROM bug_reports WHERE status = 'patched'").get().c

  // ── Posts par mois (12 derniers mois) ────────────────────────────────────
  const postsByMonth = db.prepare(`
    SELECT month, COUNT(*) AS total,
      SUM(CASE WHEN published = 1 THEN 1 ELSE 0 END) AS published
    FROM posts
    GROUP BY month
    ORDER BY month DESC
    LIMIT 12
  `).all().reverse()

  // ── Bugs par statut ───────────────────────────────────────────────────────
  const bugsByStatus = db.prepare(`
    SELECT status, COUNT(*) AS count
    FROM bug_reports
    GROUP BY status
  `).all()

  // ── Bugs par jeu ──────────────────────────────────────────────────────────
  const bugsByGame = db.prepare(`
    SELECT game_slug, COUNT(*) AS count
    FROM bug_reports
    GROUP BY game_slug
    ORDER BY count DESC
    LIMIT 5
  `).all()

  // ── Posts par jeu ─────────────────────────────────────────────────────────
  const postsByGame = db.prepare(`
    SELECT g.label_fr AS game, g.color, COUNT(pg.post_id) AS count
    FROM games g
    LEFT JOIN post_games pg ON pg.game_id = g.id
    GROUP BY g.id
    ORDER BY count DESC
  `).all()

  // ── Activité récente (20 derniers events) ────────────────────────────────
  const recentPosts = db.prepare(`
    SELECT 'post' AS type, title_fr AS label,
      CASE WHEN published = 1 THEN 'published' ELSE 'draft' END AS status,
      updated_at AS at
    FROM posts ORDER BY updated_at DESC LIMIT 7
  `).all()

  const recentBugs = db.prepare(`
    SELECT 'bug' AS type, title AS label, status, created_at AS at
    FROM bug_reports ORDER BY created_at DESC LIMIT 7
  `).all()

  const recentJobs = db.prepare(`
    SELECT 'job' AS type, title_fr AS label,
      CASE WHEN is_open = 1 THEN 'open' ELSE 'closed' END AS status,
      updated_at AS at
    FROM jobs ORDER BY updated_at DESC LIMIT 5
  `).all()

  // Fusionner + trier par date décroissante
  const recentActivity = [...recentPosts, ...recentBugs, ...recentJobs]
    .sort((a, b) => new Date(b.at) - new Date(a.at))
    .slice(0, 15)

  // ── Membres par rôle ──────────────────────────────────────────────────────
  const usersByRole = db.prepare(`
    SELECT role, COUNT(*) AS count FROM users GROUP BY role
  `).all()

  res.json({
    totals: {
      posts: totalPosts, publishedPosts, draftPosts,
      games: totalGames,
      users: totalUsers,
      jobs: totalJobs, openJobs,
      bugs: totalBugs, pendingBugs, patchedBugs,
    },
    postsByMonth,
    bugsByStatus,
    bugsByGame,
    postsByGame,
    usersByRole,
    recentActivity,
  })
})

export default router
