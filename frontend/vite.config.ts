import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg', 'favicon.ico', 'apple-touch-icon.png', 'masked-icon.svg'],
      manifest: {
        name: 'Melarium App',
        short_name: 'Melarium',
        description: 'Manage your beekeeping operations',
        theme_color: '#d97706',
        background_color: '#fffbeb',
        display: 'standalone',
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any maskable' }
        ]
      },
      workbox: {
        // Cache API responses for offline use (network-first strategy).
        // Pattern matches any /api/... path — the Vite dev proxy, the relative
        // same-origin path in production, or a full external URL if VITE_API_URL is set.
        runtimeCaching: [
          {
            urlPattern: /\/api\//i,
            handler: 'NetworkFirst',
            options: {
              cacheName: 'melarium-api-cache',
              expiration: { maxEntries: 100, maxAgeSeconds: 60 * 60 * 24 }
            }
          }
        ]
      }
    })
  ],
  build: {
    rollupOptions: {
      output: {
        // Keep every precached file under workbox's 2 MiB per-file limit: the two heaviest
        // libraries get their own vendor chunks instead of inflating the main bundle.
        manualChunks: {
          recharts: ['recharts'],
          markdown: ['react-markdown'],
        },
      },
    },
  },
  server: {
    port: 5173,
    proxy: {
      // Proxy API calls to the LOCAL .NET backend during development.
      // Pointing this at the deployed backend would make local dev mutate production data;
      // to test against production deliberately, set VITE_API_URL instead.
      //
      // Anchored regex, not the plain '/api' prefix: a string key matches every path *starting*
      // with it, so '/apiaries' — the app's landing route — was proxied to the backend and a
      // reload there answered 500 instead of loading the SPA.
      '^/api/': {
        target: 'http://localhost:62648',
        changeOrigin: true,
        secure: false
      }
    }
  }
})
