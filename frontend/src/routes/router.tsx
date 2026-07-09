import { createBrowserRouter } from 'react-router-dom'

import { AppShell } from '../app/AppShell'
import { MinigamesPage } from '../pages/MinigamesPage'
import { NotFoundPage } from '../pages/NotFoundPage'
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
        path: 'rust-knowledge',
        element: <RustKnowledgePage />,
      },
      {
        path: 'rust-knowledge/:topicId',
        element: <RustKnowledgeTopicPage />,
      },
      {
        path: '*',
        element: <NotFoundPage />,
      },
    ],
  },
])
