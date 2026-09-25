# Ocelot Control Dashboard

Management UI for the BitWrite Ocelot Control Plane. React + Vite + TypeScript SPA, styled with Tailwind CSS v4 and shadcn/ui.

Tracked by [#262](../issues/262). The bootstrap is [#432](../issues/432), the API client and auth integration is [#433](../issues/433), and the application shell is [#434](../issues/434).

## Prerequisites

- Node 20+ (developed against Node 26 / npm 12)
- The API running locally — see below

## Getting started

```bash
npm install
cp .env.example .env.local   # optional; defaults work for local dev
npm run dev
```

The dev server runs on <http://localhost:5173>.

## Talking to the API

In development the Vite dev server **proxies** `/api` to the ASP.NET Core API, so the browser sees a single origin and CORS is not involved:

```
dashboard  →  http://localhost:5173/api/...  →  proxied to  →  http://localhost:5039/api/...
```

Start the API on port 5039 (the `http` profile):

```bash
cd ..
dotnet run --project src/BitWrite.OcelotControl.Api
```

Override the target with `VITE_DEV_API_TARGET` if the API runs elsewhere. Set `VITE_API_BASE_URL` if you need the dashboard to call an absolute URL instead of the relative `/api`.

> **CORS is not configured on the API.** The dev proxy avoids it entirely. A production deployment that serves the dashboard from a different origin will need CORS enabled server-side — tracked in [#433](../issues/433).

## Scripts

| Script | Purpose |
|---|---|
| `npm run dev` | Dev server with HMR and the `/api` proxy |
| `npm run build` | Type-check (`tsc -b`) then build to `dist/` |
| `npm run preview` | Serve the production build locally |
| `npm run lint` | Lint via oxlint |

Unit tests and CI are added in the follow-up PR — see the issue comments on [#432](../issues/432) for the PR split.

## Project layout

```
dashboard/
├── components.json          # shadcn/ui config (style: radix-nova)
├── vite.config.ts           # plugins, @/ alias, /api dev proxy
├── .env.example             # documented env vars
└── src/
    ├── components/ui/       # shadcn/ui components (button, card, …)
    ├── lib/utils.ts         # cn() class merge helper
    ├── App.tsx
    └── index.css            # Tailwind import + shadcn design tokens
```

## Adding shadcn/ui components

```bash
npx shadcn@latest add <component>
```

If the CLI creates a literal `@/` directory instead of writing into `src/`, move the files and delete the stray directory — this happens when the path alias in `tsconfig.app.json` and `tsconfig.node.json` is not picked up.

## Notes

- `@/*` maps to `./src/*` in both `tsconfig.app.json` and `tsconfig.node.json`, plus `resolve.alias` in `vite.config.ts`. `baseUrl` is intentionally **not** set — TypeScript 6 deprecates it, and `paths` resolves relative to the tsconfig file.
- This project is deliberately **not** referenced from `BitWrite.OcelotControl.slnx`; that solution lists .NET projects only.
