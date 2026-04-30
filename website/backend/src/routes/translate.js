import express from 'express'

const router = express.Router()

const OPENAI_KEY = process.env.OPENAI_API_KEY

// Champs qu'on traduit dans chaque type de bloc
const TRANSLATABLE_FIELDS = {
  text:    ['content'],
  heading: ['content'],
  quote:   ['content', 'author'],
  callout: ['content'],
  image:   ['alt', 'caption'],
  video:   ['caption'],
  youtube: ['caption'],
  columns: [], // récursif géré à part
  gallery: [], // récursif sur images[].caption
  divider: [],
}

// Traduit UN seul texte via GPT — retourne la chaîne traduite
async function gptTranslateOne(text, from, to) {
  if (!OPENAI_KEY) throw new Error('OPENAI_API_KEY non définie dans les variables d\'environnement du container')
  if (!text || !text.trim()) return text

  // On remplace temporairement les \n par un token rare pour le transport.
  // GPT reçoit un texte sur une seule ligne — impossible de le tronquer.
  // On restaure les \n côté Node après réception, pas côté GPT.
  const NEWLINE_TOKEN = '__NL__'
  const BOLD_TOKEN    = '__B__'
  const ITALIC_TOKEN  = '__I__'

  // Encode les caractères spéciaux pour que GPT les traite comme du texte brut
  const singleLine = text
    .replace(/\*\*/g, BOLD_TOKEN)
    .replace(/\*/g,   ITALIC_TOKEN)
    .replace(/\n/g,   NEWLINE_TOKEN)

  const res = await fetch('https://api.openai.com/v1/chat/completions', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${OPENAI_KEY}`,
    },
    body: JSON.stringify({
      model: 'gpt-4o-mini',
      temperature: 0.2,
      response_format: { type: 'json_object' },
      messages: [
        {
          role: 'system',
          content: `Tu es un traducteur professionnel de jeux vidéo. Traduis du ${from} vers le ${to}.
Réponds UNIQUEMENT avec un JSON {"r":"<traduction>"}.
Règles :
- Traduis intégralement tout le texte fourni, du premier au dernier caractère.
- Ne traduis PAS les balises markdown (**gras**, *italique*), emoji, ni les noms propres de jeux.
- Les tokens __B__ et __I__ représentent respectivement ** et * (markdown) : conserve-les EXACTEMENT.
- Le token __NL__ représente un saut de ligne : conserve-le EXACTEMENT à sa position dans la traduction.
- Conserve la ponctuation et le ton original.`,
        },
        { role: 'user', content: singleLine },
      ],
    }),
  })

  if (!res.ok) {
    const err = await res.json().catch(() => ({}))
    throw new Error(`OpenAI error: ${err.error?.message ?? res.statusText}`)
  }

  const json = await res.json()
  try {
    const parsed = JSON.parse(json.choices[0].message.content)
    const result = (parsed.r ?? '')
      .replace(/__NL__/g, '\n')
      .replace(/__B__/g,  '**')
      .replace(/__I__/g,  '*')
    if (!result.trim()) throw new Error('empty result')
    return result
  } catch {
    console.error('[translate] parse failed:', json.choices[0].message.content)
    return text
  }
}

// Traduit tous les champs d'un bloc en parallèle, retourne le bloc traduit
async function translateBlock(block, from, to) {
  const { type, data = {} } = block
  const fields = TRANSLATABLE_FIELDS[type] ?? []
  const newData = { ...data }

  // Champs simples (content, caption, alt, author…)
  await Promise.all(
    fields.map(async f => {
      if (data[f]) newData[f] = await gptTranslateOne(data[f], from, to)
    })
  )

  // gallery : images[].caption
  if (type === 'gallery' && data.images) {
    const imgs = await Promise.all(
      data.images.map(async img => {
        if (!img.caption) return img
        return { ...img, caption: await gptTranslateOne(img.caption, from, to) }
      })
    )
    newData.images = imgs
  }

  return { ...block, data: newData }
}

// ── POST /api/translate ───────────────────────────────────────────────────
// body: { title_fr, excerpt_fr, blocks: [{type, data}] }
// → { title_en, excerpt_en, blocks: [{type, data}] }
router.post('/', async (req, res) => {
  try {
    const { title_fr, excerpt_fr, blocks = [] } = req.body

    // Titre + excerpt + chaque bloc traduits en parallèle
    const [title_en, excerpt_en, ...blocksEn] = await Promise.all([
      gptTranslateOne(title_fr   || '', 'français', 'anglais'),
      gptTranslateOne(excerpt_fr || '', 'français', 'anglais'),
      ...blocks.map(b => translateBlock(b, 'français', 'anglais')),
    ])

    res.json({ title_en, excerpt_en, blocks: blocksEn })
  } catch (e) {
    console.error('[translate]', e.message)
    res.status(500).json({ error: e.message })
  }
})

export default router
