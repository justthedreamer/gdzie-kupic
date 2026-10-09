<script setup lang="ts">
// Lets the user choose a location either via the browser Geolocation API
// ("detect automatically") or by typing an address. Emits `null` until a
// usable value is available. Use `:key` to reset it from the parent.
defineProps<{
  modelValue: LocationInputValue | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: LocationInputValue | null]
}>()

const { t } = useI18n()

type Mode = 'coords' | 'address'
type GeoState = 'idle' | 'locating' | 'found' | 'error'

const mode = ref<Mode>('coords')
const geoState = ref<GeoState>('idle')
const geoError = ref('')
const coords = ref<{ latitude: number, longitude: number } | null>(null)
const address = ref('')

function setMode(next: Mode) {
  if (mode.value === next) return
  mode.value = next

  if (next === 'address') {
    emit('update:modelValue', toLocationInputValue(address.value))
  }
  else {
    emit('update:modelValue', coords.value ? { kind: 'coords', ...coords.value } : null)
  }
}

function toLocationInputValue(raw: string): LocationInputValue | null {
  return raw.trim() ? { kind: 'address', address: raw } : null
}

function onAddressInput(raw: string) {
  address.value = raw
  emit('update:modelValue', toLocationInputValue(raw))
}

function detect() {
  if (!('geolocation' in navigator)) {
    geoState.value = 'error'
    geoError.value = t('location_input.errors.unsupported')
    return
  }

  geoState.value = 'locating'
  geoError.value = ''
  coords.value = null
  emit('update:modelValue', null)

  navigator.geolocation.getCurrentPosition(
    (position) => {
      coords.value = {
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
      }
      geoState.value = 'found'
      emit('update:modelValue', { kind: 'coords', ...coords.value })
    },
    (error) => {
      geoState.value = 'error'
      geoError.value = t(`location_input.errors.${geolocationFailure(error.code)}`)
    },
  )
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex gap-2">
      <UButton
        size="sm"
        :variant="mode === 'coords' ? 'solid' : 'outline'"
        icon="i-heroicons-map-pin-solid"
        @click="setMode('coords')"
      >
        {{ $t('location_input.mode_coords') }}
      </UButton>
      <UButton
        size="sm"
        :variant="mode === 'address' ? 'solid' : 'outline'"
        icon="i-heroicons-pencil-square"
        @click="setMode('address')"
      >
        {{ $t('location_input.mode_address') }}
      </UButton>
    </div>

    <div v-if="mode === 'coords'" class="space-y-3">
      <UButton
        variant="subtle"
        icon="i-heroicons-map-pin"
        :loading="geoState === 'locating'"
        @click="detect"
      >
        {{ geoState === 'locating' ? $t('location_input.locating') : $t('location_input.find_me') }}
      </UButton>

      <p v-if="geoState === 'found' && coords" class="text-sm text-success" role="status">
        {{ $t('location_input.found', { coords: formatCoordinates(coords.latitude, coords.longitude) }) }}
      </p>

      <UAlert
        v-if="geoState === 'error'"
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="geoError"
      />
    </div>

    <UFormField v-else :label="$t('location_input.address_label')">
      <UInput
        :model-value="address"
        :placeholder="$t('location_input.address_placeholder')"
        class="w-full"
        @update:model-value="onAddressInput(String($event))"
      />
    </UFormField>
  </div>
</template>
