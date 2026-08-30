import { StaticPage } from '../components/StaticPage'
import { getAboutCopy } from '../content/sitePages'
import { useLanguage } from '../i18n/useLanguage'

export function AboutPage() {
  const { language } = useLanguage()

  return <StaticPage copy={getAboutCopy(language)} />
}
