<script setup lang="ts">
const authStore = useAuthStore()
const localePath = useLocalePath()

const navLinks = [
  { label: 'Zapytania', to: '/requests' },
]
</script>

<template>
  <header class="border-b bg-white">
    <nav class="container mx-auto px-4 py-3 flex items-center justify-between">
      <NuxtLink :to="localePath('/')" class="text-xl font-bold tracking-tight">
        Gdzie Kupić
      </NuxtLink>

      <div class="hidden md:flex items-center gap-6">
        <NuxtLink
          v-for="link in navLinks"
          :key="link.to"
          :to="localePath(link.to)"
          class="text-sm text-gray-600 hover:text-gray-900 transition-colors"
          active-class="font-semibold text-gray-900"
        >
          {{ link.label }}
        </NuxtLink>
      </div>

      <div class="flex items-center gap-3">
        <template v-if="authStore.isAuthenticated">
          <span class="text-sm text-gray-600 hidden sm:inline">
            {{ authStore.user?.email }}
          </span>
          <UButton
            variant="ghost"
            size="sm"
            @click="authStore.clearAuth()"
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
  </header>
</template>
