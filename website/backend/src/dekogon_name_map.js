/**
 * dekogon_name_map.js
 *
 * Convertit un nom de fichier ZIP Dekogon Studios en terme de recherche Fab.com.
 *
 * Supporte deux formats :
 *   CamelCase  : "AncientRuinsVOL1"                     → "Ancient Ruins VOL.1"
 *   Avec tirets: "Abandoned Service Garage - VOL.4"      → "Abandoned Service Garage VOL.4"
 *   Mixte      : "LightingVOL2InteriorNaniteAndLowPoly"  → "Lighting VOL.2"
 *
 * Règle : on garde uniquement la série + "VOL.N", tout ce qui suit VOL.N est ignoré.
 */

/**
 * @param {string} filename  - nom du ZIP sans extension
 * @returns {string}          - terme de recherche court (série + VOL.N)
 */
export function filenameToSearchTerm(filename) {
  // 1. Normaliser tous les séparateurs en espaces
  //    underscores, tirets (avec ou sans espaces autour) → espace
  let normalized = filename
    .replace(/_/g, ' ')         // underscores → espaces
    .replace(/\s*-\s*/g, ' ')  // tirets → espace
    .replace(/\s+/g, ' ')      // espaces multiples → un seul
    .trim()

  // 2. Splitter le CamelCase sur chaque mot (y compris ceux collés après normalisation)
  normalized = normalized
    .split(' ')
    .map(word => word
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/([A-Za-z])(\d)/g, '$1 $2')
      .replace(/(\d)([A-Za-z])/g, '$1 $2')
    )
    .join(' ')
    .replace(/\s+/g, ' ')
    .trim()

  // 3. Chercher VOL (avec ou sans point) suivi d'un numéro dans la chaîne normalisée
  //    Exemples : "VOL 1", "VOL. 2", "VOL.2", "VOL 3"
  const m = normalized.match(/^(.*?)\s*\bVOL\.?\s*(\d+)/i)
  if (m) {
    const serie = m[1].trim()
    const num   = m[2]
    return `${serie} VOL.${num}`
  }

  // 4. Pas de VOL → retourne le nom normalisé
  return normalized
}
