<script setup lang="ts">
const authStore = useAuthStore()
const { t } = useI18n()

const email = computed(() => authStore.user?.email ?? '')
const name = computed(() => displayNameFromEmail(email.value))
</script>

<template>
  <aside class="hidden lg:flex lg:flex-col w-64 shrink-0 h-screen sticky top-0 gap-6 border-r border-default bg-default p-4">
    <div>
      <AppLogo />
      <p class="mt-1 text-xs text-muted">
        {{ $t('buyer_nav.role_label') }}
      </p>
    </div>

    <nav :aria-label="$t('buyer_nav.aria_main')" class="flex-1 overflow-y-auto">
      <ul class="space-y-1">
        <li v-for="item in buyerSidebarNav" :key="item.key">
          <NuxtLink
            v-if="item.to"
            :to="item.to"
            class="flex items-center gap-3 rounded-lg px-3 py-2 text-sm text-muted transition-colors hover:bg-elevated hover:text-highlighted"
            active-class="bg-primary/10 font-semibold text-primary hover:bg-primary/10 hover:text-primary"
          >
            <UIcon :name="item.icon" class="size-5 shrink-0" />
            {{ t(item.label) }}
          </NuxtLink>

          <button
            v-else
            type="button"
            disabled
            class="flex w-full cursor-not-allowed items-center gap-3 rounded-lg px-3 py-2 text-sm text-dimmed"
          >
            <UIcon :name="item.icon" class="size-5 shrink-0" />
            <span class="flex-1 text-left">{{ t(item.label) }}</span>
            <span class="text-xs">{{ $t('common.soon') }}</span>
          </button>
        </li>
      </ul>
    </nav>

    <div class="space-y-4">
      <div class="rounded-xl bg-primary/10 p-4">
        <p class="flex items-center gap-2 text-sm font-semibold text-highlighted">
          <UIcon name="i-heroicons-device-phone-mobile" class="size-5 text-primary" />
          {{ $t('buyer_nav.install_title') }}
        </p>
        <p class="mt-1 text-xs text-muted">
          {{ $t('buyer_nav.install_text') }}
        </p>
        <UButton
          size="sm"
          class="mt-3"
          block
          disabled
        >
          {{ $t('buyer_nav.install_cta') }}
        </UButton>
      </div>

      <div class="flex items-center gap-3 border-t border-default pt-4">
        <UAvatar :text="name.charAt(0)" size="md" />
        <div class="min-w-0 flex-1">
          <p class="truncate text-sm font-medium text-highlighted">
            {{ name }}
          </p>
          <p class="truncate text-xs text-muted" :title="email">
            {{ email }}
          </p>
        </div>
      </div>

      <div class="flex items-center justify-between gap-2">
        <DevAccountSwitcher />
        <UButton
          variant="ghost"
          size="sm"
          icon="i-heroicons-arrow-right-start-on-rectangle"
          @click="authStore.clearAuth()"
        >
          {{ $t('auth.logout') }}
        </UButton>
      </div>
    </div>
  </aside>
</template>
