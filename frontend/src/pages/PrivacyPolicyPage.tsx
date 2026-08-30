import { StaticPage } from '../components/StaticPage'
import { getPrivacyCopy } from '../content/sitePages'
import { useLanguage } from '../i18n/useLanguage'

export function PrivacyPolicyPage() {
  const { language } = useLanguage()

  return <StaticPage copy={getPrivacyCopy(language)} />
}
