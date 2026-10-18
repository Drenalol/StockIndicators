// Inlines the alert-image renderer bundle into a single self-contained page that the API
// loads with Microsoft.Playwright (page.SetContent). The API injects window.__SI_REQUEST__
// as an init script before any page script runs; the screenshot target is #si-root.
// The sizes below are the alert picture's box; the card (link preview) mode resizes
// #si-root from the injected request before it draws (render/main.ts).
// Run by "npm run build:render" after vite.render.config.ts produced dist/render-bundle.

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';

const bundle = readFileSync('dist/render-bundle/chart-render.js', 'utf8');

// The page is opened in a light-theme viewport: the image is always light (the app theme
// is not persisted), and chartCore reads its colors from the CSS variables below
const page = `<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>chart render</title>
<style>
  :root {
    --color-background: #ffffff;
    --color-muted-foreground: #6b7280;
    --color-border: #e5e7eb;
    --color-primary: #3b82f6;
  }
  html, body { margin: 0; padding: 0; background: #ffffff; }
  #si-root {
    position: relative;
    width: 880px;
    height: 440px;
    background: #ffffff;
    font-family: system-ui, -apple-system, sans-serif;
  }
</style>
</head>
<body>
<div id="si-root"></div>
<script>${bundle}</script>
</body>
</html>
`;

mkdirSync('dist/render', { recursive: true });
writeFileSync('dist/render/renderer.html', page);
console.log('dist/render/renderer.html written');
