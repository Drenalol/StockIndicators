import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { sentryVitePlugin } from '@sentry/vite-plugin';
import { fileURLToPath, URL } from 'node:url';
import { execSync } from 'node:child_process';
import { readFileSync, readdirSync, rmSync } from 'node:fs';
import type { Plugin } from 'vite';

const pkg = JSON.parse(readFileSync('./package.json', 'utf8'));

// The API the dev server proxies to; `dotnet run` listens on https://localhost:5051 by default
const apiDevUrl = process.env.SI_API_URL ?? 'https://localhost:5051';

let gitSha = '';
try {
  gitSha = execSync('git rev-parse --short HEAD').toString().trim();
} catch {
  /* not a git checkout (e.g. packaged build): version alone still identifies the release */
}

const appRelease = `stock-indicators-web@${pkg.version}${gitSha ? `+${gitSha}` : ''}`;

// Source maps are uploaded to GlitchTip by the plugin below; leaving them in dist
// would ship the original sources to users (the mobile shell bundles dist as-is)
const deleteSourceMaps: Plugin = {
  name: 'delete-source-maps-after-build',
  apply: 'build',
  closeBundle() {
    for (const dir of ['dist', 'dist/assets']) {
      for (const file of readdirSync(dir)) {
        if (file.endsWith('.map')) rmSync(`${dir}/${file}`);
      }
    }
  },
};

// GlitchTip speaks the Sentry protocol; uploads run only when a token is provided
const glitchtipUpload = process.env.SENTRY_AUTH_TOKEN
  ? [
      sentryVitePlugin({
        org: process.env.SENTRY_ORG || 'stock-indicators',
        project: process.env.SENTRY_PROJECT || 'stock-indicators-web',
        url: process.env.GLITCHTIP_URL || 'http://localhost:8090',
        authToken: process.env.SENTRY_AUTH_TOKEN,
        release: { name: appRelease },
      }),
    ]
  : [];

export default defineConfig({
  // deleteSourceMaps stays last: its closeBundle must run after the upload plugin's
  plugins: [react(), tailwindcss(), ...glitchtipUpload, deleteSourceMaps],
  define: {
    'import.meta.env.VITE_APP_RELEASE': JSON.stringify(appRelease),
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    // Required for readable stack traces in GlitchTip; the maps are uploaded and
    // deleted from dist by the plugin above (never shipped to users)
    sourcemap: true,
  },
  server: {
    host: true,
    port: Number(process.env.PORT ?? 5173),
    // Plain http by default. To develop behind https (webauthn and service workers want a
    // secure context), point these at your own certificate: SI_DEV_KEY / SI_DEV_CERT.
    https:
      process.env.SI_DEV_KEY && process.env.SI_DEV_CERT
        ? { key: process.env.SI_DEV_KEY, cert: process.env.SI_DEV_CERT }
        : undefined,
    proxy: {
      '/api': {
        target: apiDevUrl,
        changeOrigin: true,
        secure: false,
      },
      // SignalR chart hub: the WebSocket handshake must be upgraded too
      '/hubs': {
        target: apiDevUrl,
        changeOrigin: true,
        secure: false,
        ws: true,
      },
      // Optional error tracking under the same origin (GlitchTip speaks the Sentry
      // protocol): only used when VITE_GLITCHTIP_DSN points at a /gtip path
      '/gtip': {
        target: process.env.GLITCHTIP_URL || 'http://localhost:8090',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/gtip/, ''),
      },
    },
  },
});
