import type { Language } from '../i18n/language'

export type StaticPageSection = {
  heading: string
  body: string[]
}

export type StaticPageCopy = {
  eyebrow: string
  heading: string
  lead?: string
  intro: string
  sections: StaticPageSection[]
}

export const contactEmail = 'greyezel@gmail.com'

const aboutCopy: Record<Language, StaticPageCopy> = {
  en: {
    eyebrow: 'About',
    heading: 'About rustIQ',
    lead: 'An independent Rust server tracker built to answer one question fast: is this server worth joining right now?',
    intro: 'rustIQ is a small, independently run site focused only on Rust. It exists because general-purpose server trackers try to cover dozens of games at once, and the result is heavy pages, slow tables, and data that is not tuned to how Rust wipes actually work. rustIQ narrows the scope to a single game so the pages can stay fast and the data can be more specific.',
    sections: [
      {
        heading: 'Where the data comes from',
        body: [
          'Player counts, maps, server names and slot counts come straight from the server itself. rustIQ uses the same public Steam query that the in-game server browser uses, so nothing is second-hand from another tracker.',
          'Wipe times are harder. Some servers publish their next wipe through that same query. Many do not. For those, rustIQ reads the schedule from the public BattleMetrics page for that server. It covers wipe times only, and never stands in for live player counts or map data.',
          'Every successful check is saved. Those saved checks are what build up the population history and uptime record for each server.',
        ],
      },
      {
        heading: 'What rustIQ deliberately does not do',
        body: [
          'rustIQ does not build permanent player profiles. Current player names are shown when a server chooses to publish them, and they are treated as temporary live data, not as a searchable history of where individual people have played. Long-term player tracking adds real privacy obligations and storage cost for very little benefit to someone who is just picking a server, so the project skips it.',
          'rustIQ also does not delete servers when they go offline. A server that has been seen before keeps its history and is marked offline instead, because "this server was reachable last week and is not reachable now" is useful information. What rustIQ cannot know about is a server that went offline before it was ever discovered.',
        ],
      },
      {
        heading: 'The written guides',
        body: [
          'Alongside the tracker, rustIQ publishes a set of Rust guides covering plant genetics and crossbreeding, blueprint fragment and monument routes, base systems and automation, and console keybinds. These are written for people who already play Rust and want the mechanics explained properly rather than restated. Where a guide relies on official behaviour, the Facepunch source is linked at the bottom of the page so you can check it yourself.',
        ],
      },
      {
        heading: 'Who runs it',
        body: [
          'rustIQ is built and maintained by a single independent developer. It is not affiliated with, endorsed by, or operated by Facepunch Studios, and Rust is a trademark of Facepunch Studios. Corrections, missing servers and guide feedback are genuinely welcome at ' + contactEmail + '.',
        ],
      },
    ],
  },
  ru: {
    eyebrow: 'О проекте',
    heading: 'О rustIQ',
    lead: 'Независимый трекер серверов Rust, созданный, чтобы быстро отвечать на один вопрос: стоит ли заходить на этот сервер прямо сейчас?',
    intro: 'rustIQ — небольшой независимый сайт, посвященный только Rust. Он появился потому, что универсальные трекеры серверов пытаются охватить десятки игр сразу, и в результате получаются тяжелые страницы, медленные таблицы и данные, плохо учитывающие то, как в Rust на самом деле работают вайпы. rustIQ сужает охват до одной игры, чтобы страницы оставались быстрыми, а данные — более конкретными.',
    sections: [
      {
        heading: 'Откуда берутся данные',
        body: [
          'Онлайн, карты, названия серверов и число слотов приходят прямо с самого сервера. rustIQ использует тот же публичный запрос Steam, что и внутриигровой браузер серверов, поэтому данные не берутся из вторых рук у другого трекера.',
          'С вайпами сложнее. Некоторые серверы публикуют время следующего вайпа через этот же запрос. Многие — нет. Для них rustIQ читает расписание с публичной страницы этого сервера на BattleMetrics. Это касается только вайпов и никогда не подменяет живой онлайн или данные о карте.',
          'Каждая успешная проверка сохраняется. Именно из этих проверок складывается история онлайна и аптайм каждого сервера.',
        ],
      },
      {
        heading: 'Чего rustIQ намеренно не делает',
        body: [
          'rustIQ не строит постоянные профили игроков. Имена текущих игроков показываются, когда сервер сам их публикует, и считаются временными живыми данными, а не историей того, где играли конкретные люди. Долгосрочное отслеживание игроков создает реальные обязательства по приватности и расходы на хранение при очень малой пользе для того, кто просто выбирает сервер, поэтому проект от него отказывается.',
          'Также rustIQ не удаляет серверы, когда они уходят в офлайн. Ранее увиденный сервер сохраняет свою историю и помечается офлайн, потому что «этот сервер был доступен на прошлой неделе и недоступен сейчас» — полезная информация. Чего rustIQ знать не может, так это о серверах, которые ушли в офлайн до того, как были обнаружены.',
        ],
      },
      {
        heading: 'Письменные гайды',
        body: [
          'Помимо трекера rustIQ публикует набор гайдов по Rust: генетика растений и скрещивание, маршруты за фрагментами чертежей и монументы, базовые системы и автоматизация, бинды консоли. Они написаны для тех, кто уже играет в Rust и хочет, чтобы механики объяснили нормально, а не пересказали. Там, где гайд опирается на официальное поведение, внизу страницы указан источник Facepunch, чтобы вы могли его проверить.',
        ],
      },
      {
        heading: 'Кто ведет проект',
        body: [
          'rustIQ создан и поддерживается одним независимым разработчиком. Проект не аффилирован с Facepunch Studios, не одобрен и не управляется ими, а Rust — торговая марка Facepunch Studios. Исправления, отсутствующие серверы и отзывы о гайдах искренне приветствуются на ' + contactEmail + '.',
        ],
      },
    ],
  },
}

const contactCopy: Record<Language, StaticPageCopy> = {
  en: {
    eyebrow: 'Contact',
    heading: 'Contact rustIQ',
    lead: 'Every message goes to one inbox: ' + contactEmail + '.',
    intro: 'rustIQ is run by one person, so there is no ticket system and no contact form to fill in. Email ' + contactEmail + ' and it will be read. Including the server name or the exact page URL makes almost every request faster to act on.',
    sections: [
      {
        heading: 'Wrong or missing server data',
        body: [
          'If a server shows the wrong player count, the wrong map, a stale wipe time, or is marked offline while it is clearly up, send the server name and the rustIQ page URL. Population and map data are read live from the server over the Steam A2S protocol, so a mismatch usually means the server is blocking or rate-limiting queries, or that its query port differs from the one rustIQ discovered.',
          'Wipe times are the most common correction. When a server does not publish a schedule over A2S, rustIQ falls back to its public BattleMetrics settings page, and a schedule that was never updated there will be wrong on rustIQ too. Send the correct schedule and where it is published.',
        ],
      },
      {
        heading: 'Adding a server',
        body: [
          'rustIQ can only track servers it has discovered, so a brand new or unlisted server may simply be missing. Send the IP address and query port and it can be added to the crawler.',
        ],
      },
      {
        heading: 'Guide corrections',
        body: [
          'Rust changes constantly, and a guide that was accurate last wipe can quietly stop being accurate. If something in the Rust Knowledge section is out of date or wrong, point at the specific claim and, where possible, the source that contradicts it. Corrections that arrive with a Facepunch link get applied fastest.',
        ],
      },
      {
        heading: 'Sponsorship and advertising',
        body: [
          'Server owners and Rust-related projects interested in sponsored placement can use the same address. Mention the server or project and roughly what you have in mind.',
        ],
      },
      {
        heading: 'Privacy and data requests',
        body: [
          'rustIQ has no accounts and does not build player profiles, so there is usually little personal data to request. If you believe something published about you should not be, email the same address and describe where it appears. The Privacy Policy covers what is stored and why.',
        ],
      },
    ],
  },
  ru: {
    eyebrow: 'Контакты',
    heading: 'Связаться с rustIQ',
    lead: 'Все сообщения приходят на один адрес: ' + contactEmail + '.',
    intro: 'rustIQ ведет один человек, поэтому здесь нет системы тикетов и формы обратной связи. Напишите на ' + contactEmail + ', и письмо будет прочитано. Если указать название сервера или точный URL страницы, почти любой запрос обработается быстрее.',
    sections: [
      {
        heading: 'Неверные или отсутствующие данные о сервере',
        body: [
          'Если у сервера показан неверный онлайн, не та карта, устаревшее время вайпа или он помечен офлайн, хотя явно работает, пришлите название сервера и URL страницы на rustIQ. Онлайн и карта читаются с сервера напрямую по протоколу Steam A2S, поэтому расхождение обычно означает, что сервер блокирует или ограничивает запросы, либо его порт запросов отличается от найденного rustIQ.',
          'Чаще всего исправляют время вайпов. Когда сервер не публикует расписание через A2S, rustIQ использует его публичную страницу настроек на BattleMetrics, и расписание, которое там ни разу не обновляли, будет неверным и на rustIQ. Пришлите правильное расписание и укажите, где оно опубликовано.',
        ],
      },
      {
        heading: 'Добавление сервера',
        body: [
          'rustIQ может отслеживать только обнаруженные серверы, поэтому совсем новый или не попавший в списки сервер может просто отсутствовать. Пришлите IP-адрес и порт запросов, и его добавят в краулер.',
        ],
      },
      {
        heading: 'Исправления в гайдах',
        body: [
          'Rust постоянно меняется, и гайд, точный в прошлый вайп, может незаметно устареть. Если что-то в разделе базы знаний устарело или неверно, укажите конкретное утверждение и, по возможности, источник, который ему противоречит. Быстрее всего применяются исправления со ссылкой на Facepunch.',
        ],
      },
      {
        heading: 'Спонсорство и реклама',
        body: [
          'Владельцы серверов и проекты, связанные с Rust, могут писать на тот же адрес по поводу спонсорского размещения. Укажите сервер или проект и примерно то, что вы имеете в виду.',
        ],
      },
      {
        heading: 'Приватность и запросы по данным',
        body: [
          'В rustIQ нет учетных записей и не создаются профили игроков, поэтому персональных данных для запроса обычно почти нет. Если вы считаете, что о вас опубликовано лишнее, напишите на тот же адрес и опишите, где это появляется. Что именно хранится и зачем — см. в Политике конфиденциальности.',
        ],
      },
    ],
  },
}

const privacyCopy: Record<Language, StaticPageCopy> = {
  en: {
    eyebrow: 'Legal',
    heading: 'Privacy Policy',
    lead: 'Last updated: August 2026',
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
          'Questions about this policy can be sent to ' + contactEmail + '.',
        ],
      },
    ],
  },
  ru: {
    eyebrow: 'Правовая информация',
    heading: 'Политика конфиденциальности',
    lead: 'Обновлено: август 2026',
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
          'Вопросы по этой политике можно направлять на ' + contactEmail + '.',
        ],
      },
    ],
  },
}

export function getPrivacyCopy(language: Language = 'en') {
  return privacyCopy[language]
}

export function getAboutCopy(language: Language = 'en') {
  return aboutCopy[language]
}

export function getContactCopy(language: Language = 'en') {
  return contactCopy[language]
}
