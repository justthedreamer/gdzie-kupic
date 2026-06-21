# gdzie-kupic-ui

Frontend PWA for the **Gdzie Kupić** platform — a reverse marketplace where buyers publish purchase requests and nearby merchants respond with offers.

## Tech stack

| | |
|---|---|
| Framework | Nuxt 4 (Vue 3 + TypeScript) |
| UI | @nuxt/ui v4 (Tailwind CSS v4 + Reka UI) |
| State | Pinia |
| i18n | @nuxtjs/i18n (Polish default, English) |
| PWA | @vite-pwa/nuxt |
| Unit tests | Vitest + @nuxt/test-utils |
| E2E tests | Playwright |

## Setup

```bash
cp .env.example .env   # set NUXT_PUBLIC_API_BASE
npm install
npm run dev            # http://localhost:3000
```

## Scripts

| Command | Description |
|---|---|
| `npm run dev` | Dev server with HMR |
| `npm run build` | Production build → `.output/` |
| `npm run preview` | Preview production build |
| `npm run typecheck` | TypeScript check via vue-tsc |
| `npm run lint` | ESLint |
| `npm run format` | Prettier |
| `npm run test` | Vitest unit tests |
| `npm run test:e2e` | Playwright e2e tests |

## Docs

| Doc | Contents |
|---|---|
| [FRAMEWORK.md](FRAMEWORK.md) | Routing, components, composables, stores, i18n — Nuxt quick-reference |
| [docs/api.md](docs/api.md) | API layer: useApi, useFetch, adding new endpoints |
| [docs/theming.md](docs/theming.md) | Colour palette, dark mode, component overrides |
| [docs/testing.md](docs/testing.md) | Unit and e2e testing guide |

```

## Development Server

Start the development server on `http://localhost:3000`:

```bash
# npm
npm run dev

# pnpm
pnpm dev

# yarn
yarn dev

# bun
bun run dev
```

## Production

Build the application for production:

```bash
# npm
npm run build

# pnpm
pnpm build

# yarn
yarn build

# bun
bun run build
```

Locally preview production build:

```bash
# npm
npm run preview

# pnpm
pnpm preview

# yarn
yarn preview

# bun
bun run preview
```

Check out the [deployment documentation](https://nuxt.com/docs/getting-started/deployment) for more information.
