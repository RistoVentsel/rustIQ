import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rolldownOptions: {
      onLog(level, log, defaultHandler) {
        if (
          log.code === 'INVALID_ANNOTATION'
          && log.id?.includes('@microsoft/signalr/dist/esm/Utils.js')
        ) {
          return
        }

        defaultHandler(level, log)
      },
    },
  },
})
