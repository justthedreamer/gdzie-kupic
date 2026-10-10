<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import type { UserRole } from '~/stores/auth'
import { INBOX_REFRESH_MS } from '~/utils/chat'
import type { ShellConfig } from '~/utils/shellNav'

// App shell for authenticated pages of one role: sidebar on desktop, top bar +
// bottom tab bar on mobile. The per-role navigation comes from `config`.
const props = defineProps<{ role: UserRole, config: ShellConfig }>()

const authStore = useAuthStore()
const merchantStore = useMerchantStore()
const profileStore = useProfileStore()
const feedStore = useMerchantFeedStore()
const chatStore = useChatStore()
const route = useRoute()
const { t } = useI18n()

// Counts shown on navigation items, by item key. A merchant sees the number of
// new requests on "Requests", both roles the unread messages on "Chats", on every
// page of the shell.
const badges = computed<Record<string, number>>(() => ({
  ...(props.role === 'Merchant' ? { feed: feedStore.summary?.newCount ?? 0 } : {}),
  chats: chatStore.unread ?? 0,
}))

function loadBadges() {
  if (!authStore.user) return
  if (props.role === 'Merchant') void feedStore.loadSummary()
  if (props.role !== 'Admin') void chatStore.loadUnread()
}
onMounted(loadBadges)
watch(() => authStore.user?.id, loadBadges)
usePolling(loadBadges, INBOX_REFRESH_MS)

// The user's own first name, for the user card (and the buyer greeting).
onMounted(() => profileStore.load())
watch(() => authStore.user?.id, () => profileStore.load())

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
  (props.role === 'Merchant' ? merchantStore.profile?.name : undefined)
  ?? profileStore.firstName
  ?? displayNameFromEmail(email.value),
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
    <ShellSidebar :config="config" :name="name" :email="email" :badges="badges" />

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

    <ShellBottomNav :config="config" :badges="badges" />
  </div>
</template>
