<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import type { MockAccount } from '~/composables/useMockAccounts'

const authStore = useAuthStore()
const accounts = useMockAccounts()

async function loginAs(account: MockAccount) {
  authStore.setAuth(account.token, {
    id: account.id,
    email: account.email,
    role: account.role,
  })

  // Buyers land on their home page; other roles stay where they are.
  const home = homePathFor(account.role)
  if (home !== '/') await navigateTo(home)
}

function isActive(account: MockAccount) {
  return authStore.user?.id === account.id
}

const items = computed<DropdownMenuItem[][]>(() => [
  [{ type: 'label', label: 'Dev: zaloguj jako' }],
  accounts.map(account => ({
    label: `${account.role} — ${account.email}`,
    icon: isActive(account) ? 'i-heroicons-check-circle-solid' : undefined,
    onSelect: () => loginAs(account),
  })),
])
</script>

<template>
  <UDropdownMenu :items="items" :content="{ align: 'end' }">
    <UButton
      icon="i-heroicons-wrench-screwdriver"
      color="neutral"
      variant="subtle"
      size="sm"
    >
      Dev
    </UButton>
  </UDropdownMenu>
</template>
