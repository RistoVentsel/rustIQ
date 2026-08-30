import { createBrowserRouter } from 'react-router-dom'

import { AppShell } from '../app/AppShell'
import { AboutPage } from '../pages/AboutPage'
import { ContactPage } from '../pages/ContactPage'
import { GuessItemPage } from '../pages/GuessItemPage'
import {
  GuessServerMinigamePage,
  HigherLowerMinigamePage,
  MinigamesPage,
} from '../pages/MinigamesPage'
import { RecipeGuessPage } from '../pages/RecipeGuessPage'
import { NotFoundPage } from '../pages/NotFoundPage'
import { PrivacyPolicyPage } from '../pages/PrivacyPolicyPage'
import { RustKnowledgePage } from '../pages/RustKnowledgePage'
import { RustKnowledgeTopicPage } from '../pages/RustKnowledgeTopicPage'
import { ServerDetailPage } from '../pages/ServerDetailPage'
import { ServersPage } from '../pages/ServersPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      {
        index: true,
        element: <ServersPage />,
      },
      {
        path: 'servers/:serverId',
        element: <ServerDetailPage />,
      },
      {
        path: 'minigames',
        element: <MinigamesPage />,
      },
      {
        path: 'minigames/guess-the-server',
        element: <GuessServerMinigamePage />,
      },
      {
        path: 'minigames/higher-lower',
        element: <HigherLowerMinigamePage />,
      },
      {
        path: 'minigames/guess-the-item',
        element: <GuessItemPage />,
      },
      {
        path: 'minigames/recipe-guess',
        element: <RecipeGuessPage />,
      },
      {
        path: 'rust-knowledge',
        element: <RustKnowledgePage />,
      },
      {
        path: 'rust-knowledge/:topicId',
        element: <RustKnowledgeTopicPage />,
      },
      {
        path: 'about',
        element: <AboutPage />,
      },
      {
        path: 'contact',
        element: <ContactPage />,
      },
      {
        path: 'privacy',
        element: <PrivacyPolicyPage />,
      },
      {
        path: '*',
        element: <NotFoundPage />,
      },
    ],
  },
])
