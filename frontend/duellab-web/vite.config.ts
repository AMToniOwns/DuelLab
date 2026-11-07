import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

const api = process.env.DUELLAB_API_URL ?? 'http://localhost:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  // In development the API runs separately; in production it serves this app itself.
  server: { proxy: { '/api': api, '/health': api } },
  preview: { proxy: { '/api': api, '/health': api } },
})
