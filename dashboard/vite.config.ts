import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

// The dashboard runs on its own dev server and talks to the ASP.NET Core API
// through this proxy, so the browser sees a single origin and CORS is not needed
// in development. The target matches the `http` profile in
// src/BitWrite.OcelotControl.Api/Properties/launchSettings.json.
const apiTarget = process.env.VITE_DEV_API_TARGET ?? 'http://localhost:5039'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
})
