<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import type { UserRole } from '~/stores/auth'
import type { ShellConfig } from '~/utils/shellNav'

// App shell for authenticated pages of one role: sidebar on desktop, top bar +
// bottom tab bar on mobile. The per-role navigation comes from `config`.
const props = defineProps<{ role: UserRole, config: ShellConfig }>()

const authStore = useAuthStore()
const merchantStore = useMerchantStore()
const route = useRoute()
const { t } = useI18n()

// Signing out (or switching to another role) while on one of the role's pages
// leaves the shell: to the new role's home page, or to the landing page.
watch(
  () => authStore.user?.role,
  (role) => {
    if (role !== props.role) navigateTo(role ? homePathFor(role) : '/')
  },
)

const title = computed(() => (route.meta.shellTitleKey ? t(route.meta.shellTitleKey) : ''))
const showBack = computed(() => route.path !== props.config.homePath)

const email = computed(() => authStore.user?.email ?? '')
const name = computed(() =>
  (props.role === 'Merchant' ? merchantStore.profile?.name : undefined) ?? displayNameFromEmail(email.value),
)

const menuItems = computed<DropdownMenuItem[][]>(() => [
  [{ type: 'label', label: email.value }],
  props.config.overflow.map(item => ({
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
    <ShellSidebar :config="config" :name="name" :email="email" />

    <div class="flex min-w-0 flex-1 flex-col">
      <header class="sticky top-0 z-20 flex h-14 items-center gap-2 border-b border-default bg-default px-3 lg:hidden">
        <UButton
          v-if="showBack"
          :to="config.homePath"
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
            :aria-label="$t('shell.menu')"
          />
        </UDropdownMenu>
      </header>

      <main class="flex-1 pb-24 lg:pb-0">
        <slot />
      </main>
    </div>

    <ShellBottomNav :config="config" />
  </div>
</template>
