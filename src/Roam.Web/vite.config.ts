import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // MapLibre starts its worker as an ES module worker (see components/Map.tsx).
  worker: {
    format: 'es'
  },
  build: {
    // MapLibre alone is ~1 MB (~280 kB gzipped) and is already split into its own cached chunk.
    chunkSizeWarningLimit: 1200,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules/maplibre-gl')) {
            return 'maplibre';
          }
          if (id.includes('node_modules/@microsoft/signalr')) {
            return 'signalr';
          }
          if (id.includes('node_modules/react') || id.includes('node_modules/react-dom')) {
            return 'react-vendor';
          }
        }
      }
    }
  },
  optimizeDeps: {
    exclude: ['maplibre-gl']
  }
})

