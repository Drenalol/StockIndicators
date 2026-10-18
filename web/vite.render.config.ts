import { defineConfig } from 'vite';
import { fileURLToPath, URL } from 'node:url';

// The alert-image renderer builds as a standalone IIFE bundle (no React, no app shell):
// scripts/inline-render.mjs then inlines it into the single dist/render/renderer.html that
// the API's Playwright renderer loads. Kept a separate config so the app build, the
// source-map upload and the mobile bundle never see this entry.
export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    outDir: 'dist/render-bundle',
    emptyOutDir: true,
    sourcemap: false,
    lib: {
      entry: 'render/main.ts',
      formats: ['iife'],
      name: 'SIChartRender',
      fileName: () => 'chart-render.js',
    },
  },
});
