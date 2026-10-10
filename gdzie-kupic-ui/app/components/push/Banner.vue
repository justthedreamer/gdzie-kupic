<script setup lang="ts">
import { NOTIFICATION_SETTINGS_PATH } from '~/utils/notificationSettings'

// Invites buyers (home) and merchants (feed) to turn push on while it is still undecided, or hints
// at "add to home screen" where the browser cannot do push. Dismissal is remembered on this device.
const push = usePushStore()
const { mode, dismiss } = usePushBanner()
const { t } = useI18n()
</script>

<template>
  <UAlert
    v-if="mode"
    color="primary"
    variant="subtle"
    icon="i-heroicons-bell"
    :title="mode === 'enable' ? t('push_banner.title') : t('push_banner.install_title')"
    :description="mode === 'enable' ? t('push_banner.text') : t('push_banner.install_text')"
    :close="{ 'aria-label': t('push_banner.dismiss'), 'data-testid': 'push-banner-dismiss' }"
    role="region"
    :aria-label="mode === 'enable' ? t('push_banner.title') : t('push_banner.install_title')"
    data-testid="push-banner"
    @update:open="dismiss"
  >
    <template v-if="mode === 'enable'" #actions>
      <div class="flex flex-wrap items-center gap-2">
        <UButton size="xs" :loading="push.busy" data-testid="push-banner-enable" @click="push.enable()">
          {{ t('push_banner.enable') }}
        </UButton>
        <UButton size="xs" color="neutral" variant="outline" :to="NOTIFICATION_SETTINGS_PATH">
          {{ t('push_banner.settings') }}
        </UButton>
        <span v-if="push.error" class="text-xs text-error" role="alert">{{ t('push_banner.error') }}</span>
      </div>
    </template>
  </UAlert>
</template>
