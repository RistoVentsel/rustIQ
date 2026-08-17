import { useAdsGate } from '../hooks/useAdsGate'
import type { Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

const privacyCopy: Record<Language, {
  eyebrow: string
  heading: string
  updated: string
  intro: string
  sections: Array<{ heading: string, body: string[] }>
}> = {
  en: {
    eyebrow: 'Legal',
    heading: 'Privacy Policy',
    updated: 'Last updated: August 2026',
    intro: 'rustIQ ("we", "us") operates rustiq.gg, a Rust server tracker and community tools site. This page explains what data is collected when you visit the site and how it is used.',
    sections: [
      {
        heading: 'Data we collect',
        body: [
          'We do not require an account to use rustIQ. We store lightweight, non-identifying preferences in your browser\'s local storage, such as your selected theme and language, so the site remembers your choices between visits.',
          'Our hosting and analytics infrastructure may log standard technical data such as IP address, browser type, device type, and pages visited, for security and performance purposes.',
        ],
      },
      {
        heading: 'Advertising and cookies',
        body: [
          'rustIQ uses Google AdSense to display advertising. Google and its partners use cookies and similar technologies to serve ads based on your prior visits to this and other websites.',
          'Google\'s use of advertising cookies enables it and its partners to serve ads based on your visit to rustIQ and/or other sites on the Internet. You may opt out of personalized advertising by visiting Google\'s Ads Settings at https://adssettings.google.com. Alternatively, you can opt out of a third-party vendor\'s use of cookies for personalized advertising by visiting www.aboutads.info.',
          'If you are located in the European Economic Area, the UK, or Switzerland, you will be shown a consent banner before any non-essential cookies are set, letting you choose whether to allow personalized advertising, measurement, and similar purposes.',
        ],
      },
      {
        heading: 'Your choices',
        body: [
          'You can clear cookies and local storage at any time through your browser settings. You can also update or withdraw your advertising consent at any time using the consent management link shown on the site, where available.',
        ],
      },
      {
        heading: 'Contact',
        body: [
          'Questions about this policy can be sent to greyezel@gmail.com.',
        ],
      },
    ],
  },
  ru: {
    eyebrow: 'Правовая информация',
    heading: 'Политика конфиденциальности',
    updated: 'Обновлено: август 2026',
    intro: 'rustIQ ("мы") управляет сайтом rustiq.gg — трекером серверов Rust и набором инструментов для сообщества. На этой странице объясняется, какие данные собираются при посещении сайта и как они используются.',
    sections: [
      {
        heading: 'Какие данные мы собираем',
        body: [
          'Для использования rustIQ не нужна учетная запись. Мы храним в локальном хранилище браузера небольшие обезличенные настройки, например выбранную тему и язык, чтобы сайт запоминал ваш выбор между визитами.',
          'Наша инфраструктура хостинга и аналитики может фиксировать стандартные технические данные: IP-адрес, тип браузера, тип устройства и посещенные страницы — в целях безопасности и производительности.',
        ],
      },
      {
        heading: 'Реклама и файлы cookie',
        body: [
          'rustIQ использует Google AdSense для показа рекламы. Google и его партнеры используют файлы cookie и аналогичные технологии для показа рекламы на основе ваших предыдущих посещений этого и других сайтов.',
          'Использование Google рекламных файлов cookie позволяет ему и его партнерам показывать рекламу на основе ваших посещений rustIQ и/или других сайтов в интернете. Вы можете отказаться от персонализированной рекламы на странице https://adssettings.google.com. Также можно отказаться от использования cookie сторонними поставщиками на сайте www.aboutads.info.',
          'Если вы находитесь в Европейской экономической зоне, Великобритании или Швейцарии, перед установкой необязательных файлов cookie вам будет показан баннер согласия, позволяющий выбрать, разрешить ли персонализированную рекламу, измерения и подобные цели.',
        ],
      },
      {
        heading: 'Ваш выбор',
        body: [
          'Вы можете в любой момент очистить cookie и локальное хранилище через настройки браузера. Также вы можете в любой момент изменить или отозвать согласие на рекламу через ссылку управления согласием на сайте, если она доступна.',
        ],
      },
      {
        heading: 'Контакты',
        body: [
          'Вопросы по этой политике можно направлять на greyezel@gmail.com.',
        ],
      },
    ],
  },
}

export function PrivacyPolicyPage() {
  const { language } = useLanguage()
  const copy = privacyCopy[language]
  useAdsGate(true)

  return (
    <section className="page-stack">
      <div className="page-copy">
        <p className="page-eyebrow">{copy.eyebrow}</p>
        <h2 className="page-heading">{copy.heading}</h2>
        <p className="page-text">{copy.updated}</p>
        <p className="page-text">{copy.intro}</p>
      </div>

      {copy.sections.map((section) => (
        <div className="page-copy" key={section.heading}>
          <h3 className="page-heading">{section.heading}</h3>
          {section.body.map((paragraph) => (
            <p className="page-text" key={paragraph.slice(0, 24)}>
              {paragraph}
            </p>
          ))}
        </div>
      ))}
    </section>
  )
}