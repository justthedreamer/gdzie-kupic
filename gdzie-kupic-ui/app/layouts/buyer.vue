<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'

// App shell for authenticated Buyer pages: sidebar on desktop, top bar +
// bottom tab bar on mobile.
const authStore = useAuthStore()
const route = useRoute()
const { t } = useI18n()

// Signing out (or switching to another role) while on a buyer page leaves the shell.
watch(
  () => authStore.user?.role,
  (role) => {
    if (role !== 'Buyer') navigateTo('/')
  },
)

const title = computed(() => (route.meta.buyerTitleKey ? t(route.meta.buyerTitleKey) : ''))
const showBack = computed(() => route.path !== '/home')

const menuItems = computed<DropdownMenuItem[][]>(() => [
  [{ type: 'label', label: authStore.user?.email ?? '' }],
  buyerOverflowNav.map(item => ({
    label: t(item.label),
    icon: item.icon,
    to: item.to,
    disabled: !item.to,
  })),
  [{
    label: t('auth.logout'),
    icon: 'i-heroicons-arrow-right-start-on-rectangle',
    onSelect: () => authStore.clearAuth(),
  }],
])
</script>

<template>
  <div class="min-h-screen bg-muted lg:flex">
    <BuyerSidebar />

    <div class="flex min-w-0 flex-1 flex-col">
      <header class="sticky top-0 z-20 flex h-14 items-center gap-2 border-b border-default bg-default px-3 lg:hidden">
        <UButton
          v-if="showBack"
          to="/home"
          icon="i-heroicons-arrow-left"
          variant="ghost"
          color="neutral"
          :aria-label="$t('common.back')"
        />
        <p class="flex-1 truncate text-base font-semibold text-highlighted">
          {{ title }}
        </p>
        <DevAccountSwitcher />
        <UDropdownMenu :items="menuItems" :content="{ align: 'end' }">
          <UButton
            icon="i-heroicons-ellipsis-vertical"
            variant="ghost"
            color="neutral"
            :aria-label="$t('buyer_nav.menu')"
          />
        </UDropdownMenu>
      </header>

      <main class="flex-1 pb-24 lg:pb-0">
        <slot />
      </main>
    </div>

    <BuyerBottomNav />
  </div>
</template>
