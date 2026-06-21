# Nuxt 4 — Framework Reference

Quick-reference for this project. Not exhaustive — see [nuxt.com/docs](https://nuxt.com/docs) for the full API.

---

## Project structure

```
gdzie-kupic-ui/
├── app/                    ← srcDir (everything here is the "Vue app")
│   ├── app.vue             ← root component; holds <NuxtLayout> + <NuxtPage>
│   ├── assets/css/         ← global CSS (Tailwind + NuxtUI imported here)
│   ├── components/         ← auto-imported Vue components
│   ├── composables/        ← auto-imported composables (useXxx)
│   │   └── api/            ← domain API composables
│   ├── layouts/            ← shared page chrome (default.vue, auth.vue …)
│   ├── middleware/         ← route guards (auth.ts …)
│   ├── pages/              ← file-based routes
│   └── stores/             ← Pinia stores
├── i18n/locales/           ← translation JSON files
├── public/                 ← static assets served as-is (favicon, PWA icons)
├── tests/
│   ├── unit/               ← Vitest tests
│   └── e2e/                ← Playwright tests
└── nuxt.config.ts          ← central configuration
```

---

## Routing

Nuxt generates routes automatically from `app/pages/`. **No router file.**

| File | Route |
|---|---|
| `pages/index.vue` | `/` |
| `pages/requests/index.vue` | `/requests` |
| `pages/requests/new.vue` | `/requests/new` |
| `pages/requests/[id].vue` | `/requests/:id` (dynamic) |
| `pages/auth/login.vue` | `/auth/login` |

### Navigating

```vue
<!-- Declarative -->
<NuxtLink to="/requests">Go to requests</NuxtLink>

<!-- Programmatic (inside <script setup>) -->
await navigateTo('/requests')
await navigateTo({ name: 'requests-id', params: { id: '123' } })
```

### Reading route params / query

```ts
const route = useRoute()
const id = route.params.id        // dynamic segment [id]
const q  = route.query.search     // ?search=…
```

### Page meta (layout, middleware, title)

```ts
// at the top of any page <script setup>
definePageMeta({
  layout: 'auth',          // uses app/layouts/auth.vue
  middleware: 'auth',      // runs app/middleware/auth.ts before entering
})
```

---

## Layouts

Files in `app/layouts/` define reusable page shells.  
The active layout wraps the page's content via `<slot />`.

```
layouts/
├── default.vue    ← used automatically unless overridden
└── auth.vue       ← minimal layout for login/register
```

Switch layout per-page with `definePageMeta({ layout: 'auth' })`.

---

## Components

Drop a `.vue` file in `app/components/` — it is **auto-imported everywhere**, no `import` needed.

```
components/
├── TheHeader.vue     →  <TheHeader />       ("The" prefix = singleton)
├── AppHero.vue       →  <AppHero />
└── RequestCard.vue   →  <RequestCard />
```

Subdirectory = name prefix:

```
components/
└── Request/
    └── Card.vue      →  <RequestCard />
```

### Defining props

```ts
// no import needed — defineProps is a compiler macro
const props = defineProps<{
  title: string
  count?: number          // optional
}>()
```

---

## Composables

Files in `app/composables/` are auto-imported. The exported function name is what you call in templates/setup.

```ts
// app/composables/useGreeting.ts
export const useGreeting = (name: string) => {
  return computed(() => `Cześć, ${name}!`)
}
```

```ts
// in any page or component — no import
const greeting = useGreeting('Piotr')
```

### Key built-in composables

| Composable | Purpose |
|---|---|
| `useRoute()` | current route (params, query, path) |
| `useRouter()` | programmatic navigation |
| `useRuntimeConfig()` | env vars from `nuxt.config.ts → runtimeConfig` |
| `useSeoMeta({ title, description })` | set `<head>` meta per page |
| `useI18n()` | i18n — `const { t, locale } = useI18n()` |
| `useState('key', () => val)` | SSR-safe shared state (no store needed for simple cases) |

---

## Data fetching

Two patterns — pick based on context.

### `useFetch` — SSR + reactivity (preferred for page data)

```ts
// inside <script setup> of a page or component
const config = useRuntimeConfig()
const { data, pending, error, refresh } = await useFetch<Marketplace.Response>(
  '/api/marketplace',
  { baseURL: config.public.apiBase },
)
```

- Runs on server during SSR, hydrates on client — no flash of empty content.
- Result is a `Ref<T>`, reactive to URL changes.
- Use `refresh()` to re-fetch manually.

### `useApi()` — imperative / mutations

```ts
// for POST/PUT/DELETE or client-only fetches triggered by user action
const api = useApi()
await api.post('/api/requests', formData)
await api.del(`/api/requests/${id}`)
```

### Domain composables (extend here)

Add a file per domain under `app/composables/api/`:

```ts
// app/composables/api/useLocationApi.ts
export const useLocationApi = () => {
  const api = useApi()
  return {
    getLocation: (coords: LocationRequest) =>
      api.get<LocationResponse>('/api/location', { body: coords }),
  }
}
```

---

## State management (Pinia)

```ts
// app/stores/auth.ts
export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const isAuthenticated = computed(() => !!user.value)
  return { user, isAuthenticated }
})
```

```ts
// in any component — auto-imported
const auth = useAuthStore()
auth.user            // reactive
auth.isAuthenticated // computed
```

---

## i18n

Translation files live in `i18n/locales/pl.json` and `en.json`.

```ts
// in <script setup>
const { t, locale } = useI18n()
t('home.cta')          // → "Utwórz zapytanie"
locale.value = 'en'    // switch language
```

```html
<!-- in <template> -->
{{ $t('auth.login') }}
```

---

## Styling (NuxtUI + Tailwind v4)

- Use Tailwind utility classes directly in templates.
- NuxtUI components: `<UButton>`, `<UInput>`, `<UCard>`, `<UModal>`, `<UBadge>` etc — all auto-imported.
- To **change the primary colour**, see the commented blocks in:
  - `nuxt.config.ts` — pick a Tailwind palette name
  - `app/assets/css/main.css` — define a fully custom palette with `@theme`

---

## Scripts

```bash
npm run dev          # start dev server (http://localhost:3000)
npm run build        # production build → .output/
npm run preview      # preview production build locally
npm run lint         # ESLint
npm run format       # Prettier
npm run typecheck    # vue-tsc type check
npm run test         # Vitest unit tests
npm run test:e2e     # Playwright e2e tests
```
