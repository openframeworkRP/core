import { Helmet } from 'react-helmet-async'

const SITE_NAME = 'Small Box Studio'
const BASE_URL  = 'https://openframework.fr'
const DEFAULT_IMAGE = `${BASE_URL}/banner_site.png`

/**
 * Composant SEO réutilisable.
 *
 * Props :
 *   title       - titre de la page (sera suffixé par "| Small Box Studio")
 *   description - meta description (160 car. max recommandé)
 *   image       - URL absolue de l'image Open Graph
 *   url         - URL canonique de la page (ex: "/devblog/mon-article")
 *   type        - og:type ("website" ou "article"), défaut "website"
 *   lang        - "fr" ou "en", défaut "fr"
 *   noIndex     - true pour pages admin/privées
 *   jsonLd      - objet JSON-LD à injecter (optionnel)
 */
const DEFAULT_KEYWORDS = 'S&Box, DarkRP, Roleplay, RP, France, français, serveur RP français, OpenFramework, DarkRP français, Roleplay S&Box, S&Box France, Small Box Studio'

export default function SEO({
  title,
  description,
  keywords    = DEFAULT_KEYWORDS,
  image       = DEFAULT_IMAGE,
  url         = '',
  type        = 'website',
  lang        = 'fr',
  noIndex     = false,
  jsonLd      = null,
}) {
  const fullTitle  = title ? `${title} | ${SITE_NAME}` : `${SITE_NAME} — Serveur DarkRP & Roleplay français sur S&Box`
  const canonical  = `${BASE_URL}${url}`
  const locale     = lang === 'fr' ? 'fr_FR' : 'en_US'
  const altLocale  = lang === 'fr' ? 'en_US' : 'fr_FR'
  // Rendre l'image absolue si elle commence par / (uploads locaux)
  const absImage = image
    ? (image.startsWith('http') ? image : `${BASE_URL}${image}`)
    : DEFAULT_IMAGE

  return (
    <Helmet>
      <html lang={lang} />
      <title>{fullTitle}</title>
      {description && <meta name="description" content={description} />}
      {keywords    && <meta name="keywords"    content={keywords} />}
      <link rel="canonical" href={canonical} />
      {noIndex && <meta name="robots" content="noindex, nofollow" />}

      {/* Open Graph */}
      <meta property="og:type"                content={type} />
      <meta property="og:url"                 content={canonical} />
      <meta property="og:title"               content={fullTitle} />
      {description && <meta property="og:description" content={description} />}
      <meta property="og:image"               content={absImage} />
      <meta property="og:image:width"         content="1200" />
      <meta property="og:image:height"        content="630" />
      <meta property="og:site_name"           content={SITE_NAME} />
      <meta property="og:locale"              content={locale} />
      <meta property="og:locale:alternate"    content={altLocale} />

      {/* Twitter Card */}
      <meta name="twitter:card"        content="summary_large_image" />
      <meta name="twitter:title"       content={fullTitle} />
      {description && <meta name="twitter:description" content={description} />}
      <meta name="twitter:image"       content={absImage} />

      {/* JSON-LD structuré */}
      {jsonLd && (
        <script type="application/ld+json">
          {JSON.stringify(jsonLd)}
        </script>
      )}
    </Helmet>
  )
}
