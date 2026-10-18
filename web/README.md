# Frontend

React + TypeScript + Vite frontend for Stock Indicators (shadcn/ui, PWA).

```bash
npm install
npm run dev         # http://localhost:5173
npm run lint        # oxlint
npm run typecheck   # tsc
npm run build       # → dist/ (+ the alert-chart renderer and the static link-preview pages)
```

- The dev server proxies `/api` and `/hubs` to the .NET backend; `SI_API_URL` overrides the
  target (`https://localhost:5051` by default, which is what `dotnet run` listens on).
- Optional environment goes in `.env.local` — see `.env.example`.
- `npm run build` also builds `dist/render/` (the standalone chart renderer the backend
  screenshots alerts with) and generates the static link-preview shells from `seo/copy.json`;
  set `SEO_BASE_URL` to the domain your instance is published under before building.
- Interface texts live in `src/i18n/<locale>.json`; all nine locales must stay in sync,
  `npm run check:i18n` enforces it.
