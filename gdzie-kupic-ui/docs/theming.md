# Theming

## Semantic colours

NuxtUI maps semantic names (`primary`, `neutral`, `success`, `error` …) to actual Tailwind colour palettes. Change them in **`app/app.config.ts`**:

```ts
// app/app.config.ts
export default defineAppConfig({
  ui: {
    colors: {
      primary: 'teal',   // any Tailwind palette name, or a custom name (see below)
      neutral: 'zinc',
    },
  },
})
```

Built-in Tailwind palette names: `red` `orange` `amber` `yellow` `lime` `green` `emerald` `teal` `cyan` `sky` `blue` `indigo` `violet` `purple` `fuchsia` `pink` `rose`

No server restart required — `app.config.ts` is hot-reloaded.

---

## Custom colour palette

To use a completely custom brand colour instead of a Tailwind preset:

**1. Define all 11 shades in `app/assets/css/main.css`** using `@theme static`:

```css
@theme static {
  --color-brand-50:  oklch(…);
  --color-brand-100: oklch(…);
  /* … through 950 */
}
```

> Generate a full oklch palette at <https://uicolors.app/create>.  
> All 11 shades (50 → 950) are required.

**2. Assign in `app.config.ts`**:

```ts
colors: { primary: 'brand' }
```

The `brand` name is arbitrary — it just has to match between the CSS variable prefix and the config value.

---

## Dark mode

NuxtUI supports dark mode out of the box via the `dark:` Tailwind variant. The active mode is controlled by the `<html class="dark">` class.

To add a toggle, use `useColorMode()` from `@nuxtjs/color-mode` (already bundled with NuxtUI):

```ts
const colorMode = useColorMode()
colorMode.preference = 'dark'   // 'light' | 'dark' | 'system'
```

---

## Component-level overrides

Override any component's slots globally in `app/app.config.ts`:

```ts
export default defineAppConfig({
  ui: {
    button: {
      slots: { base: 'rounded-full font-bold' },
      defaultVariants: { color: 'neutral', variant: 'outline' },
    },
  },
})
```

Or per-instance with the `ui` prop:

```vue
<UButton :ui="{ base: 'rounded-full' }">Click</UButton>
```

Full component theme reference: <https://ui.nuxt.com/docs/getting-started/theme/components>
