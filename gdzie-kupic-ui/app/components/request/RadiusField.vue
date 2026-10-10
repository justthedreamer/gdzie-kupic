<script setup lang="ts">
import { RADIUS_PRESETS_KM } from '~/utils/postForm'

// Radius choice of the request form: presets, a custom value (> 0, no upper bound) or unlimited.
const mode = defineModel<RadiusMode>('mode', { required: true })
const custom = defineModel<string>('custom', { required: true })

defineProps<{
  error?: string
}>()

const { t } = useI18n()

const items = computed(() => [
  ...RADIUS_PRESETS_KM.map(km => ({ label: t('request.form.radius_km_value', { km }), value: String(km) })),
  { label: t('request.form.radius_custom'), value: 'custom' },
  { label: t('request.form.radius_unlimited'), value: 'unlimited' },
])
</script>

<template>
  <div class="space-y-3">
    <URadioGroup
      v-model="mode"
      :items="items"
      orientation="horizontal"
      :legend="$t('request.form.radius_label')"
      :ui="{ legend: 'sr-only', fieldset: 'flex-wrap gap-x-5 gap-y-2' }"
    />

    <UInput
      v-if="mode === 'custom'"
      v-model="custom"
      inputmode="decimal"
      :placeholder="$t('request.form.radius_custom_placeholder')"
      :aria-label="$t('request.form.radius_custom')"
      :color="error ? 'error' : undefined"
      class="w-full sm:w-48"
    >
      <template #trailing>
        <span class="text-sm text-muted">km</span>
      </template>
    </UInput>

    <p v-if="mode === 'unlimited'" class="text-sm text-muted">
      {{ $t('request.form.radius_unlimited_hint') }}
    </p>
  </div>
</template>
