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
| `npm run test` | Unit tests (Vitest, single run) |
| `npm run test:watch` | Unit tests in watch mode |
| `npm run check` | **All gates** — lint, test, build. Same three steps CI runs. |

## Project layout

```
dashboard/
├── components.json          # shadcn/ui config (style: radix-nova)
├── .oxlintrc.json           # lint config; shadcn/ui generated files exempted
├── vite.config.ts           # plugins, @/ alias, /api dev proxy
├── .env.example             # documented env vars
└── src/
    ├── components/ui/       # shadcn/ui components (button, card, …) — vendored, do not hand-edit
    ├── lib/utils.ts         # cn() class merge helper
    ├── test/                # Vitest tests + setup
    ├── App.tsx
    └── index.css            # Tailwind import + shadcn design tokens
```

## API client

`src/api/` is the typed client for the `/api/v1` surface.

```ts
import { createApiClient } from '@/api'

const client = createApiClient()

const { routes, totalCount } = await client.resources.routes.list({ page: 1, pageSize: 20 })
```

| Module | Responsibility |
|---|---|
| `http.ts` | fetch wrapper — base URL, query building, JSON, bearer token, cancellation |
| `errors.ts` | `ApiError`, and normalisation of the two error shapes the API returns |
| `resources.ts` | one module-level namespace per controller |
| `types.ts` | generated from `src/BitWrite.OcelotControl.Api/DTOs` — do not hand-edit |
| `auth.ts` | access-token seam; **the strategy is still undecided, see #433** |
| `config.ts` | reads `VITE_API_BASE_URL` |

### Two error shapes, one `ApiError`

The API returns two different failure formats and the client flattens both into a single `ApiError`:

1. `{ correlationId, error, type }` from `GlobalExceptionMiddleware`
2. `ValidationProblemDetails` — `{ type, title, status, errors: { field: [...] } }` — emitted by `[ApiController]` when model binding fails

So callers get `error.message`, `error.status`, `error.correlationId`, and `error.fieldErrors` without branching on the wire format. `error.isValidation` distinguishes the second shape, and `error.isNetworkError` means the request never reached the API.

`AbortError` is deliberately rethrown untouched, so a cancelled request is not mistaken for a network failure.

### Auth is not wired yet

`createApiClient` takes an `AccessTokenProvider`; the default sends no `Authorization` header. **This is not a finished auth story** — the API has no token-issuance endpoint, so the two options are adding one or implementing OIDC against an external IdP. See #433.

### CORS

The dev server proxies `/api`, so development needs no CORS. A cross-origin deployment does, so the API enables a named `DashboardCors` policy reading `Cors:AllowedOrigins` from configuration — origins are never wildcarded, which keeps credentialed requests possible later.

## Tests

Vitest with jsdom and Testing Library. Tests live in `src/test/` and use the `@/` alias, so they resolve modules the same way the app does.

```bash
npm run test
```

`src/test/setup.ts` loads `@testing-library/jest-dom` matchers globally.

The tests assert behaviour rather than copy — e.g. that a heading landmark exists and that generated variant classes are applied — so editing placeholder text does not break them.

## Continuous integration

`.github/workflows/dashboard.yml` runs `npm ci`, then lint, test and build on every push and PR touching `dashboard/`. This is the only workflow in the repository; the .NET solution has no CI yet ([#338](../issues/338)).

`npm ci` is used rather than `npm install` so CI installs exactly what the lockfile pins and fails if `package.json` and the lockfile drift apart.

## Adding shadcn/ui components

```bash
npx shadcn@latest add <component>
```

If the CLI creates a literal `@/` directory instead of writing into `src/`, move the files and delete the stray directory — this happens when the path alias in `tsconfig.app.json` and `tsconfig.node.json` is not picked up.

## Notes

- `@/*` maps to `./src/*` in both `tsconfig.app.json` and `tsconfig.node.json`, plus `resolve.alias` in `vite.config.ts`. `baseUrl` is intentionally **not** set — TypeScript 6 deprecates it, and `paths` resolves relative to the tsconfig file.
- This project is deliberately **not** referenced from `BitWrite.OcelotControl.slnx`; that solution lists .NET projects only.
