import { createContext, useContext, useState } from 'react'
import en from '../locales/en.json'
import fr from '../locales/fr.json'

const translations = { en, fr }

const LanguageContext = createContext()

export function LanguageProvider({ children }) {
  const [lang, setLang] = useState('fr')

  const t = (key) => {
    // key format: "nav.about" → translations[lang].nav.about
    return key.split('.').reduce((obj, k) => obj?.[k], translations[lang]) ?? key
  }

  return (
    <LanguageContext.Provider value={{ lang, setLang, t }}>
      {children}
    </LanguageContext.Provider>
  )
}

export function useLang() {
  return useContext(LanguageContext)
}
