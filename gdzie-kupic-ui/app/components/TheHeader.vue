<script setup lang="ts">
const authStore = useAuthStore()
const signOut = useSignOut()
const localePath = useLocalePath()
const { t } = useI18n()

const navLinks = computed(() => {
  const links = [{ label: t('nav.requests'), to: '/requests' }]

  switch (authStore.user?.role) {
    case 'Admin':
      links.push({ label: t('nav.catalogue'), to: '/admin/catalogue' })
      break
    case 'Buyer':
      links.push({ label: t('nav.saved_locations'), to: '/saved-locations' })
      break
    case 'Merchant':
      links.push({ label: t('nav.my_business'), to: '/merchant/subscriptions' })
      break
  }

  return links
})
</script>

<template>
  <header class="border-b border-default bg-default">
    <nav class="container mx-auto px-4 py-3 flex items-center justify-between">
      <AppLogo />

      <div class="hidden md:flex items-center gap-6">
        <NuxtLink
          v-for="link in navLinks"
          :key="link.to"
          :to="localePath(link.to)"
          class="text-sm text-muted hover:text-highlighted transition-colors"
          active-class="font-semibold text-highlighted"
        >
          {{ link.label }}
        </NuxtLink>
      </div>

      <div class="flex items-center gap-3">
        <DevAccountSwitcher />

        <template v-if="authStore.isAuthenticated">
          <span class="text-sm text-muted hidden sm:inline">
            {{ authStore.user?.email }}
          </span>
          <UButton
            variant="ghost"
            size="sm"
            @click="signOut()"
          >
            {{ $t('auth.logout') }}
          </UButton>
        </template>
        <template v-else>
          <UButton
            :to="localePath('/auth/login')"
            variant="ghost"
            size="sm"
          >
            {{ $t('auth.login') }}
          </UButton>
          <UButton
            :to="localePath('/auth/register')"
            size="sm"
          >
            {{ $t('auth.register') }}
          </UButton>
        </template>
      </div>
    </nav>

    <!-- Mobile navigation: the desktop links above are hidden below `md` -->
    <div class="md:hidden border-t border-default">
      <div class="container mx-auto px-4 py-2 flex items-center gap-4 overflow-x-auto">
        <NuxtLink
          v-for="link in navLinks"
          :key="link.to"
          :to="localePath(link.to)"
          class="text-sm text-muted hover:text-highlighted transition-colors whitespace-nowrap"
          active-class="font-semibold text-highlighted"
        >
          {{ link.label }}
        </NuxtLink>
      </div>
    </div>
  </header>
</template>
