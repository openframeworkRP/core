import Database from 'better-sqlite3'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'
import { mkdirSync } from 'fs'

const __dirname = dirname(fileURLToPath(import.meta.url))
const DB_DIR = join(__dirname, '../data')
const DB_PATH = join(DB_DIR, 'devblog.sqlite')

mkdirSync(DB_DIR, { recursive: true })

const db = new Database(DB_PATH)
db.pragma('journal_mode = DELETE')
db.pragma('foreign_keys = ON')

// ── Schema ──────────────────────────────────────────────────────────────────

db.exec(`
  CREATE TABLE IF NOT EXISTS games (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    slug      TEXT    NOT NULL UNIQUE,
    label_fr  TEXT    NOT NULL,
    label_en  TEXT    NOT NULL,
    color     TEXT,
    created_at TEXT DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS posts (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    slug       TEXT    NOT NULL UNIQUE,
    month      TEXT    NOT NULL,
    title_fr   TEXT    NOT NULL,
    title_en   TEXT    NOT NULL,
    excerpt_fr TEXT,
    excerpt_en TEXT,
    cover      TEXT,
    author     TEXT    DEFAULT 'OpenFramework',
    read_time  INTEGER DEFAULT 5,
    published  INTEGER DEFAULT 0,
    views      INTEGER DEFAULT 0,
    created_at TEXT    DEFAULT (datetime('now')),
    updated_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS post_games (
    post_id  INTEGER NOT NULL REFERENCES posts(id)  ON DELETE CASCADE,
    game_id  INTEGER NOT NULL REFERENCES games(id)  ON DELETE CASCADE,
    PRIMARY KEY (post_id, game_id)
  );

  -- Branding configurable (logo, couleurs, nom du site).
  -- Stockage key-value pour rester flexible : on peut ajouter de nouvelles
  -- cles sans migrations. Lu publiquement par le frontend au boot, ecrit
  -- uniquement par les owners via /api/branding.
  CREATE TABLE IF NOT EXISTS branding (
    key   TEXT PRIMARY KEY,
    value TEXT
  );

  CREATE TABLE IF NOT EXISTS post_views (
    post_id    INTEGER NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    ip         TEXT    NOT NULL,
    user_agent TEXT    NOT NULL DEFAULT '',
    viewed_at  TEXT    DEFAULT (datetime('now')),
    PRIMARY KEY (post_id, ip, user_agent)
  );

  CREATE TABLE IF NOT EXISTS blocks (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    post_id    INTEGER NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    lang       TEXT    NOT NULL CHECK(lang IN ('fr','en')),
    position   INTEGER NOT NULL DEFAULT 0,
    type       TEXT    NOT NULL,
    game_slug  TEXT,
    author     TEXT,
    data       TEXT    NOT NULL DEFAULT '{}',
    created_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS jobs (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    title_fr       TEXT    NOT NULL,
    title_en       TEXT    NOT NULL DEFAULT '',
    description_fr TEXT    NOT NULL DEFAULT '',
    description_en TEXT    NOT NULL DEFAULT '',
    type           TEXT    NOT NULL DEFAULT 'Bénévolat',
    game_slug      TEXT,
    contact_email  TEXT    NOT NULL DEFAULT '',
    is_open        INTEGER NOT NULL DEFAULT 1,
    created_at     TEXT    DEFAULT (datetime('now')),
    updated_at     TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS bug_reports (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    game_slug   TEXT    NOT NULL,
    title       TEXT    NOT NULL,
    description TEXT    NOT NULL DEFAULT '',
    status      TEXT    NOT NULL DEFAULT 'pending'
                        CHECK(status IN ('pending','confirmed','patched','wontfix')),
    is_public   INTEGER NOT NULL DEFAULT 0,
    reporter_ip TEXT,
    created_at  TEXT    DEFAULT (datetime('now')),
    updated_at  TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS bug_comments (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    bug_id     INTEGER NOT NULL REFERENCES bug_reports(id) ON DELETE CASCADE,
    author     TEXT    NOT NULL DEFAULT 'Team',
    content    TEXT    NOT NULL,
    created_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS users (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    steam_id     TEXT    NOT NULL UNIQUE,
    display_name TEXT    NOT NULL DEFAULT '',
    avatar       TEXT,
    role         TEXT    NOT NULL DEFAULT 'editor',
    created_at   TEXT    DEFAULT (datetime('now')),
    updated_at   TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS api_tokens (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    token_hash   TEXT    NOT NULL UNIQUE,
    name         TEXT    NOT NULL DEFAULT '',
    user_id      INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    last_used_at TEXT,
    created_at   TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS members (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    name       TEXT    NOT NULL,
    role_fr    TEXT    NOT NULL DEFAULT '',
    role_en    TEXT    NOT NULL DEFAULT '',
    grp        TEXT    NOT NULL DEFAULT 'team'
                       CHECK(grp IN ('founders','team','trial')),
    position   INTEGER NOT NULL DEFAULT 0,
    img_key    TEXT    NOT NULL DEFAULT '',
    created_at TEXT    DEFAULT (datetime('now')),
    updated_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS hub_state (
    key        TEXT PRIMARY KEY,
    value      TEXT NOT NULL,
    updated_at TEXT DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS hub_tasks (
    id          TEXT    PRIMARY KEY,
    project_id  TEXT    NOT NULL DEFAULT '',
    text        TEXT    NOT NULL DEFAULT '',
    description TEXT    NOT NULL DEFAULT '',
    category    TEXT    NOT NULL DEFAULT '',
    status      TEXT    NOT NULL DEFAULT 'todo',
    priority    INTEGER,
    assignees   TEXT    NOT NULL DEFAULT '[]',
    subtasks    TEXT    NOT NULL DEFAULT '[]',
    deadline    TEXT,
    notes       TEXT    NOT NULL DEFAULT '',
    images      TEXT    NOT NULL DEFAULT '[]',
    videos      TEXT    NOT NULL DEFAULT '[]',
    created_at  INTEGER NOT NULL DEFAULT 0,
    updated_at  INTEGER NOT NULL DEFAULT 0
  );

  CREATE TABLE IF NOT EXISTS hub_ideas (
    id          TEXT    PRIMARY KEY,
    text        TEXT    NOT NULL DEFAULT '',
    description TEXT    NOT NULL DEFAULT '',
    project_id  TEXT    NOT NULL DEFAULT '',
    comments    TEXT    NOT NULL DEFAULT '[]',
    votes       TEXT    NOT NULL DEFAULT '{}',
    created_at  INTEGER NOT NULL DEFAULT 0
  );

  CREATE TABLE IF NOT EXISTS member_tags (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    name       TEXT    NOT NULL UNIQUE,
    color      TEXT    NOT NULL DEFAULT '#888888',
    created_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS member_tag_links (
    member_id  INTEGER NOT NULL REFERENCES members(id)      ON DELETE CASCADE,
    tag_id     INTEGER NOT NULL REFERENCES member_tags(id)  ON DELETE CASCADE,
    PRIMARY KEY (member_id, tag_id)
  );

  CREATE TABLE IF NOT EXISTS hub_activity (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    action     TEXT    NOT NULL,
    detail     TEXT    NOT NULL DEFAULT '',
    author     TEXT    NOT NULL DEFAULT 'system',
    created_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS videos (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    slug       TEXT    NOT NULL UNIQUE,
    title      TEXT    NOT NULL DEFAULT '',
    filename   TEXT    NOT NULL,
    size       INTEGER NOT NULL DEFAULT 0,
    mime       TEXT    NOT NULL DEFAULT 'video/webm',
    status     TEXT    NOT NULL DEFAULT 'ready',
    created_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS images (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    slug       TEXT    NOT NULL UNIQUE,
    title      TEXT    NOT NULL DEFAULT '',
    filename   TEXT    NOT NULL,
    size       INTEGER NOT NULL DEFAULT 0,
    mime       TEXT    NOT NULL DEFAULT 'image/webp',
    width      INTEGER NOT NULL DEFAULT 0,
    height     INTEGER NOT NULL DEFAULT 0,
    created_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS rule_categories (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    type        TEXT    NOT NULL CHECK(type IN ('server', 'job', 'theme')),
    name        TEXT    NOT NULL,
    color       TEXT    NOT NULL DEFAULT '#5865f2',
    order_index INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS rules (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id INTEGER NOT NULL REFERENCES rule_categories(id) ON DELETE CASCADE,
    title       TEXT    NOT NULL DEFAULT '',
    content     TEXT    NOT NULL DEFAULT '',
    order_index INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    DEFAULT (datetime('now')),
    updated_at  TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS rule_history (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    rule_id    INTEGER NOT NULL,
    rule_title TEXT    NOT NULL DEFAULT '',
    user_name  TEXT    NOT NULL DEFAULT 'Inconnu',
    action     TEXT    NOT NULL CHECK(action IN ('created', 'updated', 'deleted')),
    old_data   TEXT,
    new_data   TEXT,
    changed_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS ui_designs (
    id         TEXT    PRIMARY KEY,
    name       TEXT    NOT NULL DEFAULT 'Sans titre',
    elements   TEXT    NOT NULL DEFAULT '[]',
    app_state  TEXT    NOT NULL DEFAULT '{}',
    files      TEXT    NOT NULL DEFAULT '{}',
    created_by TEXT    NOT NULL DEFAULT '',
    created_at TEXT    DEFAULT (datetime('now')),
    updated_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS asset_catalogue (
    id          TEXT    PRIMARY KEY,
    name        TEXT    NOT NULL DEFAULT '',
    vendor      TEXT    NOT NULL DEFAULT '',
    description TEXT    NOT NULL DEFAULT '',
    store_url   TEXT    NOT NULL DEFAULT '',
    download_url TEXT   NOT NULL DEFAULT '',
    price       TEXT    NOT NULL DEFAULT '',
    tags        TEXT    NOT NULL DEFAULT '[]',
    thumbnail   TEXT    NOT NULL DEFAULT '',
    created_at  TEXT    DEFAULT (datetime('now')),
    updated_at  TEXT    DEFAULT (datetime('now'))
  );

  -- ── Livres de règles OpenFramework ───────────────────────────────────────────
  CREATE TABLE IF NOT EXISTS sl_books (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    book_id      TEXT    NOT NULL UNIQUE,
    title        TEXT    NOT NULL DEFAULT '',
    icon         TEXT    NOT NULL DEFAULT '📖',
    cover_color  TEXT    NOT NULL DEFAULT '#1a0a00',
    cover_accent TEXT    NOT NULL DEFAULT '#D4A574',
    order_index  INTEGER NOT NULL DEFAULT 0,
    created_at   TEXT    DEFAULT (datetime('now')),
    updated_at   TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS sl_chapters (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    book_id     TEXT    NOT NULL REFERENCES sl_books(book_id) ON DELETE CASCADE,
    chapter_id  TEXT    NOT NULL,
    title       TEXT    NOT NULL DEFAULT '',
    order_index INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    DEFAULT (datetime('now')),
    updated_at  TEXT    DEFAULT (datetime('now')),
    UNIQUE(book_id, chapter_id)
  );

  CREATE TABLE IF NOT EXISTS sl_blocks (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    chapter_id  INTEGER NOT NULL REFERENCES sl_chapters(id) ON DELETE CASCADE,
    type        TEXT    NOT NULL DEFAULT 'paragraph'
                        CHECK(type IN ('heading','paragraph','note','list','rule')),
    data        TEXT    NOT NULL DEFAULT '{}',
    order_index INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    DEFAULT (datetime('now')),
    updated_at  TEXT    DEFAULT (datetime('now'))
  );

  -- ── Wiki ────────────────────────────────────────────────────────────────
  CREATE TABLE IF NOT EXISTS wiki_categories (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    type        TEXT    NOT NULL CHECK(type IN ('ingame', 'dev')),
    name        TEXT    NOT NULL,
    color       TEXT    NOT NULL DEFAULT '#5865f2',
    order_index INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS wiki_articles (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id INTEGER NOT NULL REFERENCES wiki_categories(id) ON DELETE CASCADE,
    title       TEXT    NOT NULL DEFAULT '',
    slug        TEXT    NOT NULL DEFAULT '',
    content     TEXT    NOT NULL DEFAULT '',
    published   INTEGER NOT NULL DEFAULT 0,
    order_index INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    DEFAULT (datetime('now')),
    updated_at  TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS wiki_article_history (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    article_id    INTEGER NOT NULL,
    article_title TEXT    NOT NULL DEFAULT '',
    user_name     TEXT    NOT NULL DEFAULT 'Inconnu',
    action        TEXT    NOT NULL CHECK(action IN ('created', 'updated', 'deleted')),
    old_data      TEXT,
    new_data      TEXT,
    changed_at    TEXT    DEFAULT (datetime('now'))
  );


  CREATE TABLE IF NOT EXISTS docs_pages (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    parent_id   INTEGER REFERENCES docs_pages(id) ON DELETE CASCADE,
    title       TEXT    NOT NULL DEFAULT '',
    slug        TEXT    NOT NULL DEFAULT '',
    icon        TEXT    NOT NULL DEFAULT '',
    content     TEXT    NOT NULL DEFAULT '',
    position    INTEGER NOT NULL DEFAULT 0,
    published   INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT    DEFAULT (datetime('now')),
    updated_at  TEXT    DEFAULT (datetime('now'))
  );
  CREATE INDEX IF NOT EXISTS idx_docs_pages_parent ON docs_pages(parent_id, position);

  CREATE TABLE IF NOT EXISTS docs_page_history (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    page_id    INTEGER NOT NULL,
    page_title TEXT    NOT NULL DEFAULT '',
    user_name  TEXT    NOT NULL DEFAULT 'Inconnu',
    action     TEXT    NOT NULL CHECK(action IN ('created', 'updated', 'moved', 'deleted')),
    old_data   TEXT,
    new_data   TEXT,
    changed_at TEXT    DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS gameadmin_logs (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    action         TEXT NOT NULL,
    target_steam_id TEXT,
    admin_steam_id  TEXT,
    reason         TEXT,
    extra          TEXT,
    created_at     TEXT NOT NULL DEFAULT (datetime('now'))
  );

  CREATE TABLE IF NOT EXISTS nc_dl_requests (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    file_path      TEXT NOT NULL,
    file_name      TEXT NOT NULL,
    requester_id   TEXT NOT NULL,
    requester_name TEXT,
    status         TEXT NOT NULL DEFAULT 'pending',
    reviewed_by    TEXT,
    reviewed_at    TEXT,
    created_at     TEXT NOT NULL DEFAULT (datetime('now'))
  );

  -- SteamIDs admin dans le gamemode (géré depuis le panel admin web).
  -- Polled périodiquement par WebAdminDispatcher pour peupler Client.AdminSteamIds.
  CREATE TABLE IF NOT EXISTS gamemode_admins (
    steam_id  TEXT PRIMARY KEY,
    label     TEXT NOT NULL DEFAULT '',
    added_by  TEXT NOT NULL DEFAULT '',
    added_at  TEXT DEFAULT (datetime('now'))
  );

  -- Seed des cles branding par defaut. INSERT OR IGNORE = ne touche pas
  -- les valeurs deja presentes (preserve la config de l'hebergeur).
  -- Palette par defaut : style s&box (cyan + navy sombre).
  INSERT OR IGNORE INTO branding (key, value) VALUES
    ('site_name',       'OpenFramework'),
    ('site_short_name', 'OpenFramework'),
    ('default_author',  'OpenFramework'),
    ('description',     'Framework open source pour s&box — clone, configure, joue.'),
    ('primary_color',   '#3cadd9'),
    ('accent_color',    '#88e1ff'),
    ('logo_url',        ''),
    ('favicon_url',     ''),
    -- Liens externes (header / footer) — laisse vide pour les masquer
    ('link_github',     'https://github.com/openframeworkRP/core'),
    ('link_sbox',       'https://sbox.game/openframework'),
    ('link_discord',    ''),
    ('link_steam',      '');
`)

// ── Migration : retire le CHECK constraint sur users.role ─────────────────
// Permet d'avoir des rôles personnalisés au-delà des 5 rôles système.
{
  const sqlRow = db.prepare(`SELECT sql FROM sqlite_master WHERE type='table' AND name='users'`).get()
  if (sqlRow && /CHECK\s*\(\s*role\s+IN/i.test(sqlRow.sql)) {
    db.exec(`
      CREATE TABLE users_new (
        id           INTEGER PRIMARY KEY AUTOINCREMENT,
        steam_id     TEXT    NOT NULL UNIQUE,
        display_name TEXT    NOT NULL DEFAULT '',
        avatar       TEXT,
        role         TEXT    NOT NULL DEFAULT 'editor',
        created_at   TEXT    DEFAULT (datetime('now')),
        updated_at   TEXT    DEFAULT (datetime('now'))
      );
      INSERT INTO users_new (id, steam_id, display_name, avatar, role, created_at, updated_at)
        SELECT id, steam_id, display_name, avatar, role, created_at, updated_at FROM users;
      DROP TABLE users;
      ALTER TABLE users_new RENAME TO users;
    `)
  }
}

// ── Migrations douces (colonnes ajoutées après création initiale) ─────────
const blockCols = db.prepare("PRAGMA table_info(blocks)").all().map(c => c.name)
if (!blockCols.includes('author')) {
  db.exec("ALTER TABLE blocks ADD COLUMN author TEXT")
}
const memberCols = db.prepare("PRAGMA table_info(members)").all().map(c => c.name)
if (!memberCols.includes('steam_id64')) {
  db.exec("ALTER TABLE members ADD COLUMN steam_id64 TEXT NOT NULL DEFAULT ''")
}
const videoCols = db.prepare("PRAGMA table_info(videos)").all().map(c => c.name)
if (!videoCols.includes('status')) {
  db.exec("ALTER TABLE videos ADD COLUMN status TEXT NOT NULL DEFAULT 'ready'")
}

// ── Migration douce : contenu Markdown par chapitre ───────────────────────
const slChapterCols = db.prepare("PRAGMA table_info(sl_chapters)").all().map(c => c.name)
if (!slChapterCols.includes('content')) {
  db.exec("ALTER TABLE sl_chapters ADD COLUMN content TEXT NOT NULL DEFAULT ''")
}

const ideaCols = db.prepare("PRAGMA table_info(hub_ideas)").all().map(c => c.name)
if (!ideaCols.includes('description')) {
  db.exec("ALTER TABLE hub_ideas ADD COLUMN description TEXT NOT NULL DEFAULT ''")
}

const taskCols = db.prepare("PRAGMA table_info(hub_tasks)").all().map(c => c.name)
if (!taskCols.includes('created_by')) {
  db.exec("ALTER TABLE hub_tasks ADD COLUMN created_by TEXT NOT NULL DEFAULT ''")
}
if (!taskCols.includes('updated_by')) {
  db.exec("ALTER TABLE hub_tasks ADD COLUMN updated_by TEXT NOT NULL DEFAULT ''")
}
if (!taskCols.includes('videos')) {
  db.exec("ALTER TABLE hub_tasks ADD COLUMN videos TEXT NOT NULL DEFAULT '[]'")
}

// Migration priority TEXT → INTEGER (1..5, nullable). Anciennes valeurs low/med/high
// repassent à NULL pour forcer une re-priorisation explicite par l'équipe.
{
  const prioCol = db.prepare("PRAGMA table_info(hub_tasks)").all().find(c => c.name === 'priority')
  if (prioCol && prioCol.type !== 'INTEGER') {
    db.exec(`
      ALTER TABLE hub_tasks ADD COLUMN priority_new INTEGER;
      UPDATE hub_tasks SET priority_new = NULL;
      ALTER TABLE hub_tasks DROP COLUMN priority;
      ALTER TABLE hub_tasks RENAME COLUMN priority_new TO priority;
    `)
  }
}

const activityCols = db.prepare("PRAGMA table_info(hub_activity)").all().map(c => c.name)
if (!activityCols.includes('target_type')) {
  db.exec("ALTER TABLE hub_activity ADD COLUMN target_type TEXT NOT NULL DEFAULT ''")
}
if (!activityCols.includes('target_id')) {
  db.exec("ALTER TABLE hub_activity ADD COLUMN target_id TEXT NOT NULL DEFAULT ''")
}

// ── Migration : hub blob → hub_tasks + hub_ideas + hub_state(misc) ────────
{
  const already = db.prepare("SELECT 1 FROM hub_state WHERE key = 'hub_migrated'").get()
  if (!already) {
    const hubRow = db.prepare("SELECT value FROM hub_state WHERE key = 'hub'").get()
    let blob = {}
    if (hubRow) {
      try { blob = JSON.parse(hubRow.value) } catch { blob = {} }
    }

    const tasks  = Array.isArray(blob.tasks)  ? blob.tasks  : []
    const ideas  = Array.isArray(blob.ideas)  ? blob.ideas  : []
    const misc   = {
      milestones:     blob.milestones     ?? [],
      mapAnnotations: blob.mapAnnotations ?? {},
      fabAssets:      blob.fabAssets      ?? [],
      fabStudios:     blob.fabStudios     ?? [],
    }

    const insertTask = db.prepare(`
      INSERT OR IGNORE INTO hub_tasks
        (id, project_id, text, description, category, status, priority,
         assignees, subtasks, deadline, notes, images, created_at, updated_at)
      VALUES
        (@id, @project_id, @text, @description, @category, @status, @priority,
         @assignees, @subtasks, @deadline, @notes, @images, @created_at, @updated_at)
    `)
    const insertIdea = db.prepare(`
      INSERT OR IGNORE INTO hub_ideas (id, text, project_id, comments, votes, created_at)
      VALUES (@id, @text, @project_id, @comments, @votes, @created_at)
    `)
    const upsertMisc = db.prepare(`
      INSERT INTO hub_state (key, value, updated_at)
      VALUES ('misc', ?, datetime('now'))
      ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
    `)

    db.transaction(() => {
      for (const t of tasks) {
        insertTask.run({
          id:          t.id ?? `t_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`,
          project_id:  t.projectId  ?? t.project_id  ?? '',
          text:        t.text        ?? '',
          description: t.description ?? '',
          category:    t.category    ?? '',
          status:      t.status      ?? 'todo',
          priority:    typeof t.priority === 'number' ? t.priority : null,
          assignees:   JSON.stringify(t.assignees  ?? []),
          subtasks:    JSON.stringify(t.subtasks   ?? []),
          deadline:    t.deadline    ?? null,
          notes:       t.notes       ?? '',
          images:      JSON.stringify(t.images     ?? []),
          created_at:  t.createdAt   ?? t.created_at   ?? Date.now(),
          updated_at:  t.updatedAt   ?? t.updated_at   ?? Date.now(),
        })
      }
      for (const i of ideas) {
        insertIdea.run({
          id:         i.id ?? `i_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`,
          text:       i.text        ?? '',
          project_id: i.projectId   ?? i.project_id ?? '',
          comments:   JSON.stringify(i.comments ?? []),
          votes:      JSON.stringify(i.votes    ?? {}),
          created_at: i.createdAt   ?? i.created_at  ?? Date.now(),
        })
      }
      upsertMisc.run(JSON.stringify(misc))
      db.prepare(`INSERT INTO hub_state (key, value, updated_at) VALUES ('hub_migrated', '1', datetime('now'))`).run()
    })()
  }
}

// ── Seed : jeux par défaut ────────────────────────────────────────────────

const gamesCount = db.prepare('SELECT COUNT(*) as c FROM games').get().c
if (gamesCount === 0) {
  db.prepare(`
    INSERT INTO games (slug, label_fr, label_en, color)
    VALUES (?, ?, ?, ?)
  `).run('core', 'OpenFramework', 'OpenFramework', '#e07b39')
}

// ── Seed : users par défaut ───────────────────────────────────────────────
// Source de verite : variable d'env ALLOWED_STEAM_IDS (CSV) — chaque
// SteamID listé devient automatiquement 'owner'. Ca permet au wizard
// d'installation d'enregistrer le 1er admin sans intervention manuelle
// dans la DB.
//
// Pour ajouter un admin a la volee : edite ALLOWED_STEAM_IDS dans .env
// puis 'docker compose up -d --force-recreate website.api'.
const ALLOWED_STEAM_IDS = (process.env.ALLOWED_STEAM_IDS || '')
  .split(',')
  .map(s => s.trim())
  .filter(Boolean)

const DEFAULT_USERS = ALLOWED_STEAM_IDS.map(steamId => [steamId, 'Owner', 'owner'])

const upsertUser = db.prepare(`
  INSERT INTO users (steam_id, display_name, role)
  VALUES (?, ?, ?)
  ON CONFLICT(steam_id) DO UPDATE SET
    -- Force toujours le role owner pour les SteamIDs dans
    -- ALLOWED_STEAM_IDS — c'est la source de verite.
    role = excluded.role
`)

for (const [steamId, displayName, role] of DEFAULT_USERS) {
  upsertUser.run(steamId, displayName, role)
}

if (DEFAULT_USERS.length > 0) {
  console.log(`✅  Seed users : ${DEFAULT_USERS.length} owner(s) depuis ALLOWED_STEAM_IDS`)
}

// ── Purge des owners residuels qui ne sont plus dans ALLOWED_STEAM_IDS ──
// ALLOWED_STEAM_IDS est la source de verite. Tout user qui est encore
// 'owner' en DB mais pas liste est demote 'viewer' (on ne le supprime pas
// pour preserver d'eventuelles relations — devblog posts, members, etc.).
//
// Pour les nouveaux deploiements c'est inutile. Pour les installs
// existantes (cas du fork OpenFramework depuis l'ancien repo small_life),
// ca purge automatiquement les anciens owners hardcodes au prochain boot.
if (ALLOWED_STEAM_IDS.length > 0) {
  const placeholders = ALLOWED_STEAM_IDS.map(() => '?').join(',')
  const result = db.prepare(`
    UPDATE users SET role = 'viewer'
    WHERE role = 'owner' AND steam_id NOT IN (${placeholders})
  `).run(...ALLOWED_STEAM_IDS)
  if (result.changes > 0) {
    console.log(`🧹 Purge owners : ${result.changes} ancien(s) owner(s) demote(s) en viewer (pas dans ALLOWED_STEAM_IDS)`)
  }
}

// ── Seed : devlogs par défaut (désactivé — décommenter pour re-seeder) ─────
/*
const SEED_POSTS = [
  {
    slug: 'devlog-mars-2026',
    month: '2026-03',
    title_fr: 'Devlog #1 — Mars 2026',
    title_en: 'Devlog #1 — March 2026',
    excerpt_fr: "Premier devlog mensuel du studio. Au programme : les fondations de OpenFramework et les grandes décisions d'architecture.",
    excerpt_en: 'First monthly devlog from the studio. On the agenda: OpenFramework foundations and key architecture decisions.',
    author: 'OpenFramework',
    read_time: 5,
    published: 1,
    games: ['core'],
    blocksFr: [
      { game_slug: null, type: 'callout', data: { variant: 'info', content: "👋 Bienvenue dans le premier devlog mensuel ! Ce journal regroupe toutes nos avancées. Utilisez les filtres ci-dessus pour ne voir que ce qui vous intéresse." } },
      { game_slug: 'core', type: 'heading', data: { level: 2, content: '🏙️ OpenFramework' } },
      { game_slug: 'core', type: 'text',    data: { content: "Ce mois-ci nous avons posé les bases techniques de **OpenFramework**. La priorité était de définir une architecture solide avant d'ajouter du contenu." } },
      { game_slug: 'core', type: 'heading', data: { level: 3, content: 'Système de jobs' } },
      { game_slug: 'core', type: 'text',    data: { content: "Nous avons implémenté un système de jobs **entièrement dynamiques**. Les joueurs peuvent choisir leur rôle en temps réel : citoyen, policier, criminel… Chaque job dispose de *permissions* et d'outils uniques." } },
      { game_slug: 'core', type: 'callout', data: { variant: 'success', content: '✅ Jobs disponibles au lancement : Citoyen, Policier, Criminel, Médecin, Dealer.' } },
      { game_slug: 'core', type: 'heading', data: { level: 3, content: 'Économie persistante' } },
      { game_slug: 'core', type: 'text',    data: { content: "Un système d'économie **persistante** a été intégré. L'argent est sauvegardé entre les sessions et l'économie de la ville est cohérente." } },
      { game_slug: null, type: 'divider', data: {} },
      { game_slug: null, type: 'text',    data: { content: "C'est tout pour ce mois-ci. Le mois prochain on attaque la **map** et les bâtiments interactifs. Stay tuned !" } },
    ],
    blocksEn: [
      { game_slug: null, type: 'callout', data: { variant: 'info', content: "👋 Welcome to the first monthly devlog! This journal covers all our progress. Use the filters above to focus on what interests you." } },
      { game_slug: 'core', type: 'heading', data: { level: 2, content: '🏙️ OpenFramework' } },
      { game_slug: 'core', type: 'text',    data: { content: "This month we laid the technical groundwork for **OpenFramework**. The priority was a solid architecture before adding content." } },
      { game_slug: 'core', type: 'heading', data: { level: 3, content: 'Job System' } },
      { game_slug: 'core', type: 'text',    data: { content: "We implemented a fully **dynamic job system**. Players can choose their role in real time: citizen, police officer, criminal… Each job has unique *permissions* and tools." } },
      { game_slug: 'core', type: 'callout', data: { variant: 'success', content: '✅ Jobs available at launch: Citizen, Police, Criminal, Doctor, Dealer.' } },
      { game_slug: 'core', type: 'heading', data: { level: 3, content: 'Persistent Economy' } },
      { game_slug: 'core', type: 'text',    data: { content: "A **persistent economy** system has been integrated. Money is saved between sessions." } },
      { game_slug: null, type: 'divider', data: {} },
      { game_slug: null, type: 'text',    data: { content: "That's it for this month. Next month we'll tackle the **map** and interactive buildings. Stay tuned!" } },
    ],
  },
  {
    slug: 'devlog-fevrier-2026',
    month: '2026-02',
    title_fr: 'Devlog #2 — Février 2026',
    title_en: 'Devlog #2 — February 2026',
    excerpt_fr: "La map de OpenFramework prend forme : zones résidentielles, quartier d'affaires et premier PNJ interactif.",
    excerpt_en: "OpenFramework's map takes shape: residential areas, business district and the first interactive NPC.",
    author: 'OpenFramework',
    read_time: 4,
    published: 1,
    games: ['core'],
    blocksFr: [
      { game_slug: null, type: 'callout', data: { variant: 'info', content: "📅 Devlog de février — utilisez les filtres pour ne voir que le jeu qui vous intéresse." } },
      { game_slug: 'core', type: 'heading', data: { level: 2, content: '🏙️ OpenFramework' } },
      { game_slug: 'core', type: 'text',    data: { content: "Février a été un mois très **visuel**. On a commencé à peupler la map avec des bâtiments authentiques et des zones distinctes." } },
      { game_slug: 'core', type: 'heading', data: { level: 3, content: 'Zones résidentielles' } },
      { game_slug: 'core', type: 'text',    data: { content: "Les appartements sont maintenant **achetables**. Les joueurs peuvent décorer leur intérieur et inviter d'autres joueurs." } },
      { game_slug: 'core', type: 'quote',   data: { content: "L'appartement, c'est le point de départ de toute vie dans la ville.", author: 'Lead Designer' } },
      { game_slug: 'core', type: 'heading', data: { level: 3, content: 'PNJ interactif' } },
      { game_slug: 'core', type: 'text',    data: { content: "Le premier PNJ — le **banquier** — est en place. Il permet de déposer de l'argent et de contracter des prêts." } },
      { game_slug: 'core', type: 'callout', data: { variant: 'warning', content: "⚠️ Le système de prêts est encore en beta. Les intérêts seront équilibrés avant le lancement." } },
      { game_slug: null, type: 'divider', data: {} },
      { game_slug: null, type: 'text',    data: { content: "Rendez-vous en mars pour le prochain devlog !" } },
    ],
    blocksEn: [
      { game_slug: null, type: 'callout', data: { variant: 'info', content: "📅 February devlog — use the filters to focus on the game you're interested in." } },
      { game_slug: 'core', type: 'heading', data: { level: 2, content: '🏙️ OpenFramework' } },
      { game_slug: 'core', type: 'text',    data: { content: "February was a very **visual** month. We started populating the map with authentic buildings and distinct zones." } },
      { game_slug: 'core', type: 'heading', data: { level: 3, content: 'Residential Areas' } },
      { game_slug: 'core', type: 'text',    data: { content: "Apartments are now **purchasable**. Players can decorate their interiors and invite other players." } },
      { game_slug: 'core', type: 'quote',   data: { content: "The apartment is the starting point of every life in the city.", author: 'Lead Designer' } },
      { game_slug: 'core', type: 'heading', data: { level: 3, content: 'Interactive NPC' } },
      { game_slug: 'core', type: 'text',    data: { content: "The first NPC — the **banker** — is in place. He allows depositing money and taking out loans." } },
      { game_slug: 'core', type: 'callout', data: { variant: 'warning', content: "⚠️ The loan system is still in beta. Interest rates will be balanced before official launch." } },
      { game_slug: null, type: 'divider', data: {} },
      { game_slug: null, type: 'text',    data: { content: "See you in March for the next devlog!" } },
    ],
  },
]

const insertPost = db.prepare(`
  INSERT INTO posts (slug, month, title_fr, title_en, excerpt_fr, excerpt_en, author, read_time, published)
  VALUES (@slug, @month, @title_fr, @title_en, @excerpt_fr, @excerpt_en, @author, @read_time, @published)
`)
const insertPostGame = db.prepare(`
  INSERT OR IGNORE INTO post_games (post_id, game_id)
  SELECT ?, g.id FROM games g WHERE g.slug = ?
`)
const insertBlock = db.prepare(`
  INSERT INTO blocks (post_id, lang, position, type, game_slug, data)
  VALUES (?, ?, ?, ?, ?, ?)
`)

const seedPosts = db.transaction(() => {
  for (const p of SEED_POSTS) {
    const existing = db.prepare('SELECT id FROM posts WHERE slug = ?').get(p.slug)
    if (existing) continue

    const { blocksFr, blocksEn, games, ...meta } = p
    const result = insertPost.run(meta)
    const postId = result.lastInsertRowid

    for (const gameSlug of (games || [])) {
      insertPostGame.run(postId, gameSlug)
    }
    for (let i = 0; i < blocksFr.length; i++) {
      const b = blocksFr[i]
      insertBlock.run(postId, 'fr', i, b.type, b.game_slug ?? null, JSON.stringify(b.data))
    }
    for (let i = 0; i < blocksEn.length; i++) {
      const b = blocksEn[i]
      insertBlock.run(postId, 'en', i, b.type, b.game_slug ?? null, JSON.stringify(b.data))
    }
  }
})
seedPosts()
*/

// ── Migration one-shot : wiki type='dev' → docs_pages ────────────────────────
// Copie une seule fois les articles du wiki dev existants dans la nouvelle table docs,
// puis marque les catégories wiki source comme migrées pour ne jamais rejouer.
{
  const already = db.prepare("SELECT COUNT(*) AS n FROM docs_pages").get().n
  const pendingCats = db.prepare(
    "SELECT * FROM wiki_categories WHERE type = 'dev' AND name NOT LIKE '[migré]%' ORDER BY order_index, id"
  ).all()

  if (already === 0 && pendingCats.length > 0) {
    const insertPage = db.prepare(
      `INSERT INTO docs_pages (parent_id, title, slug, content, position, published)
       VALUES (?, ?, ?, ?, ?, ?)`
    )
    const markCatMigrated = db.prepare(
      "UPDATE wiki_categories SET name = '[migré] ' || name WHERE id = ?"
    )
    function slugify(text) {
      return String(text || '')
        .normalize('NFD').replace(/[̀-ͯ]/g, '')
        .toLowerCase().trim()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '')
    }

    db.transaction(() => {
      let catPos = 0
      for (const cat of pendingCats) {
        const catRes = insertPage.run(
          null, cat.name, slugify(cat.name) || `section-${cat.id}`,
          '', catPos++, 1,
        )
        const parentId = catRes.lastInsertRowid

        const articles = db.prepare(
          "SELECT * FROM wiki_articles WHERE category_id = ? ORDER BY order_index, id"
        ).all(cat.id)

        let artPos = 0
        for (const a of articles) {
          insertPage.run(
            parentId, a.title || 'Sans titre',
            slugify(a.title) || a.slug || `page-${a.id}`,
            a.content || '', artPos++, a.published,
          )
        }
        markCatMigrated.run(cat.id)
      }
    })()
  }
}

// ── Migrations pour colonnes ajoutées après la création initiale ──────────────
try { db.exec(`ALTER TABLE posts ADD COLUMN views INTEGER DEFAULT 0`) } catch {}
try {
  // Recrée post_views avec user_agent si absente ou ancienne version
  const hasUA = db.prepare(`PRAGMA table_info(post_views)`).all().some(c => c.name === 'user_agent')
  if (!hasUA) {
    db.exec(`DROP TABLE IF EXISTS post_views`)
  }
  db.exec(`
    CREATE TABLE IF NOT EXISTS post_views (
      post_id    INTEGER NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
      ip         TEXT    NOT NULL,
      user_agent TEXT    NOT NULL DEFAULT '',
      viewed_at  TEXT    DEFAULT (datetime('now')),
      PRIMARY KEY (post_id, ip, user_agent)
    )
  `)
} catch {}

// ── Seed : livres OpenFramework (exécuté une seule fois) ────────────────────
{
  const alreadySeeded = db.prepare("SELECT 1 FROM sl_books WHERE book_id = 'server'").get()
  if (!alreadySeeded) {
    const insertBook    = db.prepare(`INSERT OR IGNORE INTO sl_books (book_id, title, icon, cover_color, cover_accent, order_index) VALUES (?, ?, ?, ?, ?, ?)`)
    const insertChapter = db.prepare(`INSERT OR IGNORE INTO sl_chapters (book_id, chapter_id, title, order_index) VALUES (?, ?, ?, ?)`)
    const insertBlock   = db.prepare(`INSERT OR IGNORE INTO sl_blocks (chapter_id, type, data, order_index) VALUES (?, ?, ?, ?)`)

    const seedData = [
      {
        book_id: 'server', title: 'Règlement Général', icon: '📜',
        cover_color: '#1a0a00', cover_accent: '#D4A574', order_index: 0,
        chapters: [
          {
            id: 'introduction', title: 'Introduction & Principes', order_index: 0,
            blocks: [
              { type: 'paragraph', data: { text: 'Bienvenue sur OpenFramework. Ce règlement s\'applique à tous les joueurs sans exception.' } },
              { type: 'note', data: { text: 'Le non-respect des règles peut entraîner des sanctions allant du simple avertissement au bannissement définitif.' } },
            ],
          },
          {
            id: 'conduite', title: 'Règles de Conduite', order_index: 1,
            blocks: [
              { type: 'rule', data: { number: 1, title: 'Respect mutuel', text: 'Tout joueur doit se comporter de façon respectueuse envers les autres joueurs et les membres du staff.' } },
              { type: 'rule', data: { number: 2, title: 'Interdiction du harcèlement', text: 'Tout comportement harcelant, discriminatoire ou abusif est strictement interdit.' } },
            ],
          },
        ],
      },
      {
        book_id: 'police', title: 'Manuel de Police', icon: '🚔',
        cover_color: '#0a0f1a', cover_accent: '#6ea8fe', order_index: 1,
        chapters: [
          {
            id: 'recrutement', title: 'Recrutement & Formation', order_index: 0,
            blocks: [
              { type: 'paragraph', data: { text: 'Ce manuel est destiné aux agents du LSPD de OpenFramework.' } },
              { type: 'rule', data: { number: 1, title: 'Conditions d\'admission', text: 'Tout candidat doit avoir au moins 10 heures de jeu sur le serveur avant de candidater.' } },
            ],
          },
          {
            id: 'procedures', title: 'Procédures d\'Intervention', order_index: 1,
            blocks: [
              { type: 'rule', data: { number: 1, title: 'Code d\'alerte', text: 'Code 1 : Situation non urgente. Code 2 : Urgence modérée. Code 3 : Urgence absolue.' } },
              { type: 'note', data: { text: 'Toujours annoncer son code sur la radio avant d\'intervenir.' } },
            ],
          },
        ],
      },
    ]

    db.transaction(() => {
      for (const book of seedData) {
        insertBook.run(book.book_id, book.title, book.icon, book.cover_color, book.cover_accent, book.order_index)
        for (const ch of book.chapters) {
          insertChapter.run(book.book_id, ch.id, ch.title, ch.order_index)
          const chRow = db.prepare('SELECT id FROM sl_chapters WHERE book_id = ? AND chapter_id = ?').get(book.book_id, ch.id)
          for (let i = 0; i < ch.blocks.length; i++) {
            insertBlock.run(chRow.id, ch.blocks[i].type, JSON.stringify(ch.blocks[i].data), i)
          }
        }
      }
    })()
  }
}

export default db
