import { StaticPage } from '../components/StaticPage'
import { getContactCopy } from '../content/sitePages'
import { useLanguage } from '../i18n/useLanguage'

export function ContactPage() {
  const { language } = useLanguage()

  return <StaticPage copy={getContactCopy(language)} />
}
