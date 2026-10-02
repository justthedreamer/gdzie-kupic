# GdzieKupic — UI Design System

This is the canonical design system for the GdzieKupic UI. It defines the design tokens
(colors, typography, radius, shadows) and component guidelines used across the app, and is
implemented in [`app/assets/css/main.css`](../app/assets/css/main.css) and
[`app/app.config.ts`](../app/app.config.ts).

It complements [`theming.md`](./theming.md), which explains the *mechanism* of styling
(NuxtUI/Tailwind) — this document defines *what values* are actually used.

## Sources

| File | Scope |
|---|---|
| [`ui/buyer.png`](./ui/buyer.png) | Dashboard, new request, requests feed, live status, chat (buyer and merchant views) |
| [`ui/merchant.png`](./ui/merchant.png) | Merchant panel — responding to requests (have it / may have it / can't help), mobile chat, coverage map |
| [`ui/admin.png`](./ui/admin.png) | Admin panel — data table, filters, edit panel (slide-over), toggle, delete |
| [`ui/sign.png`](./ui/sign.png) | Login, registration (buyer/merchant/admin), location onboarding, branch and tag subscription setup |

---

## 1. Overview

GdzieKupic is a modern marketplace platform connecting buyers with local merchants. The visual
identity is based on a clean, minimalist and functional interface that communicates trust,
simplicity, accessibility and efficiency.

The UI uses a predominantly neutral color palette with **emerald green** as the primary brand
color.

### Design principles

- Minimalist and clean interface.
- White surfaces on a very light grey background.
- Emerald green used for primary actions and active states.
- Subtle borders and soft shadows.
- Rounded corners and consistent spacing.
- Clear typography and strong visual hierarchy.
- Consistent components across desktop and mobile.
- Accessibility and readability as priorities.

---

## 2. Typography

**Primary font:** [Inter](https://fonts.google.com/specimen/Inter) (sans-serif) — chosen for its
excellent readability on screens, neutral appearance and versatility across UI components.

### Font weights

| Weight | Name | Usage |
|---|---|---|
| 400 | Regular | Body text, descriptions, inputs |
| 500 | Medium | Navigation, secondary headings, labels |
| 600 | SemiBold | Buttons, card titles, section headings |
| 700 | Bold | Main headings and important information |

### Typography scale

| Element | Font size | Weight |
|---|---|---|
| H1 | 28px | 700 |
| H2 | 20px | 600 |
| H3 | 16px | 600 |
| Body | 14px | 400 |
| Body Medium | 14px | 500 |
| Small text | 12px | 400 |
| Caption | 11px | 400 |
| Button | 14px | 600 |

### Typography rules

- Use Inter consistently throughout the application.
- Avoid unnecessary font weight variations.
- Use Regular for most descriptive content.
- Use Medium for navigation and labels.
- Use SemiBold for interactive elements and headings.
- Maintain sufficient contrast between primary and secondary text.
- Avoid using font sizes below 11px.

### Implementation

Inter is loaded via Google Fonts and wired as Tailwind's default sans font (so it applies via
the `font-sans` utility and as the base body font) in
[`main.css`](../app/assets/css/main.css):

```css
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');

@theme static {
  --font-sans: 'Inter', sans-serif;
}
```

> **Decision:** keep the Google Fonts `@import` for now, instead of adding the
> [`@nuxt/fonts`](https://fonts.nuxt.com/) module to self-host Inter. Revisit later if
> performance/privacy requirements make self-hosting necessary.

---

## 3. Color Palette

> Swatches are rendered with [shields.io](https://shields.io) static badges, since GitHub strips
> inline `style` attributes from raw HTML (so a plain colored `<div>` won't render in the GitHub
> web UI). They display correctly both on GitHub and in the VS Code Markdown preview.

The color system is built around emerald green, neutral whites and subtle grey tones. The
primary green represents the brand identity and should be used consistently across all
application modules.

### 3.1 Primary brand colors

| Token | Swatch | Hex | Usage |
|---|---|---|---|
| Primary | ![#087F60](https://img.shields.io/badge/-087F60-087F60?style=flat-square) | `#087F60` | Main brand color, primary buttons |
| Primary Hover | ![#066B51](https://img.shields.io/badge/-066B51-066B51?style=flat-square) | `#066B51` | Hover and pressed states |
| Primary Light | ![#E8F4EF](https://img.shields.io/badge/-E8F4EF-E8F4EF?style=flat-square) | `#E8F4EF` | Active backgrounds, selected elements |
| Primary Subtle | ![#F2F8F5](https://img.shields.io/badge/-F2F8F5-F2F8F5?style=flat-square) | `#F2F8F5` | Soft green surfaces |

**Implementation:** NuxtUI needs a full `50`–`950` Tailwind-style scale to color `primary`
components (solid buttons, active nav items, rings, dark mode, …), not just one hex value. A
full scale was generated from the exact brand color (`#087F60`, via
[uicolors.app/create](https://uicolors.app/create)) and registered as a new custom color named
`brand` in [`main.css`](../app/assets/css/main.css):

| Step | Hex | Swatch |
|---|---|---|
| 50 | `#ECFDF5` | ![#ECFDF5](https://img.shields.io/badge/-ECFDF5-ECFDF5?style=flat-square) |
| 100 | `#D2F9E6` | ![#D2F9E6](https://img.shields.io/badge/-D2F9E6-D2F9E6?style=flat-square) |
| 200 | `#A9F1D2` | ![#A9F1D2](https://img.shields.io/badge/-A9F1D2-A9F1D2?style=flat-square) |
| 300 | `#71E4B9` | ![#71E4B9](https://img.shields.io/badge/-71E4B9-71E4B9?style=flat-square) |
| 400 | `#39CE9B` | ![#39CE9B](https://img.shields.io/badge/-39CE9B-39CE9B?style=flat-square) |
| 500 | `#15B483` | ![#15B483](https://img.shields.io/badge/-15B483-15B483?style=flat-square) |
| 600 | `#09926B` | ![#09926B](https://img.shields.io/badge/-09926B-09926B?style=flat-square) |
| **700** | **`#087F60`** *(= exact brand Primary)* | ![#087F60](https://img.shields.io/badge/-087F60-087F60?style=flat-square) |
| 800 | `#095C47` | ![#095C47](https://img.shields.io/badge/-095C47-095C47?style=flat-square) |
| 900 | `#084C3C` | ![#084C3C](https://img.shields.io/badge/-084C3C-084C3C?style=flat-square) |
| 950 | `#032B22` | ![#032B22](https://img.shields.io/badge/-032B22-032B22?style=flat-square) |

```css
@theme static {
  --color-brand-50: #ecfdf5;
  --color-brand-100: #d2f9e6;
  --color-brand-200: #a9f1d2;
  --color-brand-300: #71e4b9;
  --color-brand-400: #39ce9b;
  --color-brand-500: #15b483;
  --color-brand-600: #09926b;
  --color-brand-700: #087f60; /* exact brand Primary */
  --color-brand-800: #095c47;
  --color-brand-900: #084c3c;
  --color-brand-950: #032b22;
}
```

```ts
// app/app.config.ts
export default defineAppConfig({
  ui: { colors: { primary: 'brand', neutral: 'zinc' } },
})
```

> ⚠️ **Important:** our exact brand green landed at the **`700`** step of the generated scale,
> not Tailwind's conventional `500`. NuxtUI resolves `bg-primary`/`text-primary`/etc. from
> `--ui-primary`, which defaults to `--ui-color-primary-500` in light mode — so without an
> override, solid buttons would render as `#15B483` (step `500`), not the intended `#087F60`.
> `main.css` fixes this with the officially-documented override:
>
> ```css
> :root {
>   --ui-primary: var(--ui-color-primary-700);
> }
> ```
>
> Dark mode keeps NuxtUI's default (`--ui-color-primary-400`, i.e. `#39CE9B`) — worth a visual
> check once dark mode is implemented, but not changed here.
>
> Also note: NuxtUI's solid-button hover state doesn't swap to a darker shade number — it
> applies `hover:bg-primary/75` (75% opacity) instead. So `Primary Hover` (`#066B51`) won't be
> used verbatim by NuxtUI components; it remains available as `--color-primary-hover` for custom
> (non-NuxtUI) elements that want the literal hex.

### 3.2 Background colors

| Token | Swatch | Hex | Usage |
|---|---|---|---|
| Background | ![#F7F9F8](https://img.shields.io/badge/-F7F9F8-F7F9F8?style=flat-square) | `#F7F9F8` | Main application background |
| Surface | ![#FFFFFF](https://img.shields.io/badge/-FFFFFF-FFFFFF?style=flat-square) | `#FFFFFF` | Cards, modals, panels |
| Surface Secondary | ![#F3F5F3](https://img.shields.io/badge/-F3F5F3-F3F5F3?style=flat-square) | `#F3F5F3` | Secondary surfaces |
| Surface Hover | ![#F8FAF9](https://img.shields.io/badge/-F8FAF9-F8FAF9?style=flat-square) | `#F8FAF9` | Hover states |

### 3.3 Text colors

| Token | Swatch | Hex | Usage |
|---|---|---|---|
| Text Primary | ![#2A322E](https://img.shields.io/badge/-2A322E-2A322E?style=flat-square) | `#2A322E` | Main headings and body text |
| Text Secondary | ![#66716C](https://img.shields.io/badge/-66716C-66716C?style=flat-square) | `#66716C` | Descriptions and secondary information |
| Text Muted | ![#98A39E](https://img.shields.io/badge/-98A39E-98A39E?style=flat-square) | `#98A39E` | Placeholder text and muted labels |
| Text Inverse | ![#FFFFFF](https://img.shields.io/badge/-FFFFFF-FFFFFF?style=flat-square) | `#FFFFFF` | Text on dark backgrounds |

### 3.4 Border colors

| Token | Swatch | Hex | Usage |
|---|---|---|---|
| Border | ![#E7ECE9](https://img.shields.io/badge/-E7ECE9-E7ECE9?style=flat-square) | `#E7ECE9` | Default borders |
| Border Strong | ![#D5DEDA](https://img.shields.io/badge/-D5DEDA-D5DEDA?style=flat-square) | `#D5DEDA` | Inputs and emphasized borders |
| Border Focus | ![#087F60](https://img.shields.io/badge/-087F60-087F60?style=flat-square) | `#087F60` | Focused inputs and interactive elements |

> Note: `neutral` stays set to `zinc` in `app.config.ts` (NuxtUI still needs a named palette for
> component props like `color="neutral"`), but NuxtUI's neutral *text/background/border
> utilities* (`text-highlighted`, `text-muted`, `bg-default`, `bg-muted`, `border-default`, …) are
> re-pointed at the tokens above instead of zinc's stock stops, via `--ui-text-*`/`--ui-bg-*`/
> `--ui-border-*` overrides in [`main.css`](../app/assets/css/main.css). Zinc's cool, slightly
> blue-grey undertone (e.g. `zinc-900 #18181B` for headings) read noticeably off next to this
> warm, green-tinted neutral scale — most visible on headings — so it's no longer used for text,
> background or border rendering, only for component color props that require a named palette.

### 3.5 Status colors

Status colors communicate system feedback and merchant responses.

| Status | Main color | Background | Usage |
|---|---|---|---|
| Success | ![#16A05D](https://img.shields.io/badge/-16A05D-16A05D?style=flat-square) `#16A05D` | ![#EAF6EF](https://img.shields.io/badge/-EAF6EF-EAF6EF?style=flat-square) `#EAF6EF` | Available, online, confirmed |
| Warning | ![#F59E0B](https://img.shields.io/badge/-F59E0B-F59E0B?style=flat-square) `#F59E0B` | ![#FFF5E8](https://img.shields.io/badge/-FFF5E8-FFF5E8?style=flat-square) `#FFF5E8` | Pending, checking availability |
| Danger | ![#EF4444](https://img.shields.io/badge/-EF4444-EF4444?style=flat-square) `#EF4444` | ![#FFF0F0](https://img.shields.io/badge/-FFF0F0-FFF0F0?style=flat-square) `#FFF0F0` | Unavailable, errors |
| Neutral | ![#92999B](https://img.shields.io/badge/-92999B-92999B?style=flat-square) `#92999B` | ![#F2F3F4](https://img.shields.io/badge/-F2F3F4-F2F3F4?style=flat-square) `#F2F3F4` | No response, inactive |

> `warning` (`#F59E0B`) and `danger` (`#EF4444`) are an exact match for Tailwind's
> `amber-500`/`red-500` (NuxtUI defaults) — no override needed. `success` (`#16A05D`) is close
> enough to `green-600` (`#16a34a`) that the stock palette is used as-is too.

### 3.6 Color usage guidelines

- Primary green should be reserved for important actions and active states.
- White should remain the dominant surface color.
- Light grey should be used for backgrounds and visual separation.
- Avoid excessive use of saturated colors.
- Status colors should only communicate meaningful system states.
- Do not use status colors as decorative elements.
- Maintain sufficient text contrast against all backgrounds.

Recommended visual balance: **85–90% neutral colors**, **10–15% brand and status colors**.

---

## 4. Design Tokens — CSS Variables

All colors and typography are managed through centralized design tokens in
[`main.css`](../app/assets/css/main.css):

```css
:root {
  --ui-primary: var(--ui-color-primary-700);
  --ui-radius: 6px;

  --ui-text-highlighted: var(--color-text-primary);
  --ui-text: var(--color-text-primary);
  --ui-text-toned: var(--color-text-secondary);
  --ui-text-muted: var(--color-text-secondary);
  --ui-text-dimmed: var(--color-text-muted);

  --ui-bg: var(--color-surface);
  --ui-bg-muted: var(--color-background);
  --ui-bg-elevated: var(--color-surface-secondary);

  --ui-border: var(--color-border);
  --ui-border-muted: var(--color-border);
  --ui-border-accented: var(--color-border-strong);

  /* Brand (exact hex from the design system, independent of the generated `brand` scale) */
  --color-primary: #087f60;
  --color-primary-hover: #066b51;
  --color-primary-light: #e8f4ef;
  --color-primary-subtle: #f2f8f5;

  /* Backgrounds */
  --color-background: #f7f9f8;
  --color-surface: #ffffff;
  --color-surface-secondary: #f3f5f3;
  --color-surface-hover: #f8faf9;

  /* Text (toned down from the original #171c1a — same warm neutral hue, softer than near-black) */
  --color-text-primary: #2a322e;
  --color-text-secondary: #66716c;
  --color-text-muted: #98a39e;
  --color-text-inverse: #ffffff;

  /* Borders */
  --color-border: #e7ece9;
  --color-border-strong: #d5deda;
  --color-border-focus: #087f60;

  /* Status */
  --color-success: #16a05d;
  --color-success-bg: #eaf6ef;
  --color-warning: #f59e0b;
  --color-warning-bg: #fff5e8;
  --color-danger: #ef4444;
  --color-danger-bg: #fff0f0;
  --color-neutral: #92999b;
  --color-neutral-bg: #f2f3f4;

  /* Shadows */
  --shadow-card: 0 2px 12px rgba(20, 40, 30, 0.04);
  --shadow-hover: 0 4px 16px rgba(20, 40, 30, 0.08);
  --shadow-modal: 0 12px 32px rgba(20, 40, 30, 0.1);
}
```

Use the plain `--color-*`/`--shadow-*` variables for any custom (non-NuxtUI) styling. For
NuxtUI components and native semantic utilities (`<UButton color="primary">`, `text-highlighted`,
`bg-muted`, `border-default`, …) nothing extra is needed — they already resolve to the same
tokens via the `--ui-*` overrides above and `app.config.ts`.

---

## 5. Border Radius System

Rather than disconnected custom tokens, border radius is driven by NuxtUI's own unified
`--ui-radius` variable, which scales all `rounded-*` Tailwind utilities
(`rounded-sm`, `rounded-md`, `rounded-lg`, `rounded-xl`, …):

```css
:root {
  --ui-radius: 6px;
}
```

| Target (design system) | Value | Closest Tailwind utility (`--ui-radius: 6px`) |
|---|---|---|
| Small | 6px | `rounded-sm` (6px) |
| Medium | 10px | `rounded-md` (9px) |
| Large | 14px | `rounded-lg` (12px) |
| Extra Large | 18px | `rounded-xl` (18px) |

Use the standard `rounded-sm`/`rounded-md`/`rounded-lg`/`rounded-xl` Tailwind classes directly —
no separate `--radius-*` custom properties are needed, and NuxtUI components pick this up
automatically.

---

## 6. Shadows

The interface uses subtle shadows to create depth without making the UI visually heavy. Borders
should provide most of the visual separation between elements; shadows are a secondary cue.

| Token | Value | Usage |
|---|---|---|
| Card | `0 2px 12px rgba(20, 40, 30, 0.04)` | Default card elevation |
| Hover | `0 4px 16px rgba(20, 40, 30, 0.08)` | Hover elevation |
| Modal | `0 12px 32px rgba(20, 40, 30, 0.10)` | Modals, slide-overs |

---

## 7. Component Guidelines

### Buttons

- **Primary (solid)** — background `Primary` (`#087F60`), white text, weight 600,
  `border-radius: 10px`; hover uses `Primary Hover` (`#066B51`). Full width in forms
  (`Create account`, `Publish Request`, `Log in`).
- **Secondary/outline** — white background, `Text Primary` text, `Border` outline; hover uses a
  light grey background (`Cancel`, `Skip for now`, `Sign in with Google`).
- **Destructive** — outline, red text/border (`Delete` in the admin panel).
- **FAB (mobile)** — solid `Primary`, circular, `+` icon, shadow, centered in the bottom nav bar.

### Cards

- Background: white (`Surface`).
- Border: `#E7ECE9`.
- Border radius: 14px.
- Shadow: subtle card shadow (`--shadow-card`).
- Consistent internal spacing; avoid heavy borders and strong shadows.

### Inputs

- Background: white, border `#E7ECE9`, border radius 8–10px.
- Font size: 14px, text color `Text Primary`.
- Focus border: `Primary` (`#087F60`).
- Placeholder color: `Text Muted`.

### Navigation

- Consistent icon sizes across the app.
- **Sidebar (desktop):** active items use a light green background (`Primary Light`) with
  `Primary`-colored icon/label; inactive items use `Text Secondary`.
- **Bottom nav (mobile):** icons + a centered FAB; the active tab is highlighted in `Primary`.
- Navigation labels use 14px Medium.

### Status indicators / badges

- Colored dot alongside a text label, pill shaped.
- Light background tinted with the matching status color (e.g. `Live`/`New`/`Active` → success
  green; `Inactive` → danger red).
- Keep status indicators compact and consistent.

### Response option cards

Pattern from `merchant.png` — 3 variants styled by semantic color (have it / may have it /
can't help): a large icon in a circle on the left, title + description in the middle, a status
icon on the right; the entire card background is tinted with a light shade of that color.

### Forms

- Label above the input.
- **Segmented control** (e.g. "Use my location" / "Enter manually"): the active option = light
  `Primary`-tinted background + `Primary` border + `Primary` text.
- **Tag chips** (selectable): inactive = `Border` outline; active = `Primary Light` background +
  `Primary` border + checkmark icon.
- Inline checkbox + link (`Terms of Service`, `Privacy Policy`) in `Primary`.

### Chat

- Own messages: solid `Primary` background, white text, `rounded-2xl`.
- Other party's messages: white background, `Border` outline, `rounded-2xl`.
- Composer bar: fully rounded, grey background, attachment/camera icons, a circular `Primary`
  send button.

### Illustrations

Simple line illustrations in `Primary` (a pin with a radius circle, a storefront) on a light,
circular background — used on onboarding screens (`sign.png`).

---

## 8. Responsive Design

The design system supports:

- Desktop dashboard layouts.
- Tablet layouts.
- Mobile application layouts.
- Responsive forms and cards.
- Consistent typography across screen sizes.
- Touch-friendly interactive elements.

Desktop and mobile interfaces maintain the same brand identity, typography and color system.

---

## 9. Implementation Rules

When creating or modifying UI components:

1. Always use the defined design tokens.
2. Do not introduce new colors without documenting them here.
3. Use Inter as the default font family.
4. Maintain consistent spacing, borders and corner radii.
5. Prefer reusable components over duplicated styles.
6. Maintain consistency between buyer, merchant and administrator interfaces.
7. Ensure all interactive states are visually distinguishable.
8. Follow accessibility and contrast best practices.
9. Keep the interface minimal and avoid unnecessary decoration.
10. Any new UI component must follow this design system.

---

## 10. Status

### Implemented

- [x] `primary` set to a fully custom `brand` palette (50–950, generated from the exact
      `#087F60` anchor — no stock-Tailwind approximation left)
      ([`app.config.ts`](../app/app.config.ts), [`main.css`](../app/assets/css/main.css)).
- [x] `--ui-primary` pinned to `--ui-color-primary-700` so NuxtUI's solid/active/ring states
      render in the exact brand green instead of Tailwind's conventional `500` step.
- [x] `neutral` stays `zinc` for component `color` props, but its `text-*`/`bg-*`/`border-*`
      utilities are re-pointed at this design system's own tokens via `--ui-text-*`/`--ui-bg-*`/
      `--ui-border-*` overrides (zinc's cool undertone read too stark/dark on headings).
- [x] Inter wired as the default `font-sans` + Google Fonts import (`main.css`).
- [x] Border radius driven by NuxtUI's unified `--ui-radius` (set to `6px`) instead of
      disconnected custom tokens.
- [x] Full token set (backgrounds, text, borders, status, shadows) added as CSS custom
      properties in `main.css`.
- [x] Removed the previous unused `--color-brand-*` placeholder palette (was an unused
      blue/violet hue).
- [x] **Decision:** keep loading Inter via the Google Fonts `@import` for now — revisit
      `@nuxt/fonts` later if self-hosting becomes necessary (perf/privacy).
- [x] **Decision:** force light mode for now (`ui: { colorMode: false }` in `nuxt.config.ts`) —
      dark mode isn't themed yet, so letting the browser/OS preference switch to it produced
      inconsistent, half-themed results. Revisit once dark mode is designed properly.

### Open questions

- [ ] Dark mode currently keeps NuxtUI's default `--ui-color-primary-400` (`#39CE9B`) for
      `--ui-primary` — do a visual pass once dark mode UI is implemented to confirm contrast and
      brand feel; may need its own override (e.g. `--ui-color-primary-300` or `-200`).
- [ ] NuxtUI's solid-button hover uses `hover:bg-primary/75` (opacity), not a separate shade — so
      the literal `Primary Hover` hex (`#066B51`) only applies where it's used explicitly via
      `--color-primary-hover` in custom (non-NuxtUI) markup, not inside `<UButton>` etc. Flag if
      exact hover-hex parity with NuxtUI components turns out to matter.
