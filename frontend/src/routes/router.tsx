import { createBrowserRouter } from 'react-router-dom'

import { AppShell } from '../app/AppShell'
import {
  GuessServerMinigamePage,
  GuessItemMinigamePage,
  HigherLowerMinigamePage,
  MinigamesPage,
  RecipeGuessMinigamePage,
} from '../pages/MinigamesPage'
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
        element: <GuessItemMinigamePage />,
      },
      {
        path: 'minigames/recipe-guess',
        element: <RecipeGuessMinigamePage />,
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
