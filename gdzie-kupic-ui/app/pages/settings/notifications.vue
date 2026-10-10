<script setup lang="ts">
import { pushSwitchView } from '~/utils/notificationSettings'

// One page for buyers and merchants, inside the shell of the signed-in role (the inline
// middleware picks the layout; admins are sent away by the `role` middleware).
definePageMeta({
  layout: 'buyer',
  middleware: [
    'auth',
    'role',
    () => {
      if (useAuthStore().user?.role === 'Merchant') setPageLayout('merchant')
      else setPageLayout('buyer')
    },
  ],
  roles: ['Buyer', 'Merchant'],
  shellTitleKey: 'notification_settings.title',
})

const { t } = useI18n()
useSeoMeta({ title: () => `${t('notification_settings.title')} | Gdzie Kupić` })

const authStore = useAuthStore()
const push = usePushStore()
const settings = useNotificationSettings()

const pushView = computed(() => pushSwitchView(push.state))
const emailHint = computed(() =>
  authStore.user?.role === 'Merchant'
    ? t('notification_settings.email.merchant_hint')
    : t('notification_settings.email.buyer_hint'),
)

// Switching on asks for the permission, which the browser only allows right after a user action:
// `enable()` is called straight from the switch.
function togglePush(on: boolean) {
  return on ? push.enable() : push.disable()
}

onMounted(() => {
  void push.refresh()
  void settings.load()
})
</script>

<template>
  <div class="container mx-auto max-w-2xl space-y-6 px-4 py-6 lg:py-8">
    <div>
      <h1 class="text-2xl font-bold text-highlighted">
        {{ $t('notification_settings.title') }}
      </h1>
      <p class="mt-1 text-sm text-muted">
        {{ $t('notification_settings.subtitle') }}
      </p>
    </div>

    <UCard>
      <section class="space-y-3" aria-labelledby="push-settings-title">
        <h2 id="push-settings-title" class="font-semibold text-highlighted">
          {{ $t('notification_settings.push.title') }}
        </h2>

        <USwitch
          :model-value="pushView.checked"
          :disabled="pushView.disabled || push.busy"
          :loading="push.busy"
          :label="$t('notification_settings.push.label')"
          data-testid="push-switch"
          @update:model-value="togglePush"
        />

        <p class="text-sm text-muted" data-testid="push-hint">
          {{ $t(`notification_settings.push.hint.${pushView.hint}`) }}
        </p>

        <UAlert
          v-if="push.error"
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="$t('notification_settings.push.error')"
          role="alert"
          data-testid="push-error"
        />
      </section>
    </UCard>

    <UCard>
      <section class="space-y-3" aria-labelledby="email-settings-title">
        <h2 id="email-settings-title" class="font-semibold text-highlighted">
          {{ $t('notification_settings.email.title') }}
        </h2>

        <p v-if="settings.loading.value" class="text-sm text-muted">
          {{ $t('common.loading') }}
        </p>

        <div v-else-if="settings.loadFailed.value" class="space-y-3" data-testid="email-load-error">
          <UAlert
            color="error"
            variant="subtle"
            icon="i-heroicons-exclamation-circle"
            :description="$t('notification_settings.email.load_error')"
          />
          <UButton variant="outline" @click="settings.load()">
            {{ $t('common.retry') }}
          </UButton>
        </div>

        <template v-else>
          <USwitch
            :model-value="settings.emailEnabled.value"
            :disabled="settings.saving.value"
            :loading="settings.saving.value"
            :label="$t('notification_settings.email.label')"
            data-testid="email-switch"
            @update:model-value="settings.setEmailEnabled($event)"
          />

          <p class="text-sm text-muted" data-testid="email-hint">
            {{ emailHint }}
          </p>

          <UAlert
            v-if="settings.saveFailed.value"
            color="error"
            variant="subtle"
            icon="i-heroicons-exclamation-circle"
            :description="$t('notification_settings.email.save_error')"
            role="alert"
            data-testid="email-save-error"
          />
        </template>
      </section>
    </UCard>
  </div>
</template>
