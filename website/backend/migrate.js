/**
 * migrate.js — Importe les posts de blogPosts.js dans la DB SQLite
 * Usage : node migrate.js
 * À lancer UNE fois après avoir démarré le container Docker.
 */

const API = process.env.API_URL || 'http://localhost:3001'

const GAMES_DATA = [
  { slug: 'core', label_fr: 'OpenFramework', label_en: 'OpenFramework', color: '#e07b39' },
]

const POSTS_DATA = [
]

async function post(path, body) {
  const r = await fetch(`${API}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  const json = await r.json()
  if (!r.ok) throw new Error(json.error || r.statusText)
  return json
}

async function run() {
  console.log(`\n🚀 Migration → ${API}\n`)

  // 1. Créer les jeux manquants
  const existingGames = await fetch(`${API}/api/games`).then(r => r.json())
  for (const g of GAMES_DATA) {
    if (!existingGames.find(e => e.slug === g.slug)) {
      await post('/api/games', g)
      console.log(`  ✅ Jeu créé : ${g.slug}`)
    } else {
      console.log(`  ⏭  Jeu déjà présent : ${g.slug}`)
    }
  }

  // 2. Créer les posts + blocs
  const existingPosts = await fetch(`${API}/api/posts?all=1`).then(r => r.json())

  for (const p of POSTS_DATA) {
    if (existingPosts.find(e => e.slug === p.slug)) {
      console.log(`  ⏭  Post déjà présent : ${p.slug}`)
      continue
    }

    const { blocksFr, blocksEn, ...meta } = p
    const created = await post('/api/posts', meta)
    console.log(`  ✅ Post créé : ${created.slug} (id ${created.id})`)

    // Blocs FR
    for (let i = 0; i < blocksFr.length; i++) {
      await post(`/api/posts/${created.id}/blocks`, { lang: 'fr', position: i, ...blocksFr[i] })
    }
    // Blocs EN
    for (let i = 0; i < blocksEn.length; i++) {
      await post(`/api/posts/${created.id}/blocks`, { lang: 'en', position: i, ...blocksEn[i] })
    }
    console.log(`     └─ ${blocksFr.length} blocs FR + ${blocksEn.length} blocs EN insérés`)
  }

  console.log('\n✅ Migration terminée !\n')
}

run().catch(e => { console.error('❌', e.message); process.exit(1) })
