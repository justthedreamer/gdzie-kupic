<script setup lang="ts">
// Lets the user choose a location either via the browser Geolocation API
// ("detect automatically", after an explicit consent) or by typing an address
// (postal code and city required, street and house number optional) and
// searching for it. Emits the resolved coordinates, or `null` while nothing is
// resolved. Use `:key` to reset it from the parent.
defineProps<{
  modelValue: LocationInputValue | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: LocationInputValue | null]
}>()

const { t } = useI18n()
const { searchAddress } = useLocationApi()

interface Resolved {
  source: 'geolocation' | 'address'
  value: LocationInputValue
  message: string
}

const resolved = ref<Resolved | null>(null)
const errorMessage = ref('')
const locating = ref(false)
const searching = ref(false)
const consentOpen = ref(false)
let searchRun = 0

const fields = reactive<AddressFields>({ postalCode: '', city: '', street: '', houseNumber: '' })

const searchable = computed(() => isAddressSearchable(fields))
const postalCodeError = computed(() =>
  fields.postalCode.trim() && !isValidPostalCode(fields.postalCode) ? t('location_input.errors.postal_code') : undefined,
)

function setResolved(next: Resolved | null) {
  resolved.value = next
  emit('update:modelValue', next ? next.value : null)
}

// Editing the address invalidates a previous search result (a detected location is kept).
watch(fields, () => {
  searchRun++
  searching.value = false
  errorMessage.value = ''
  if (resolved.value?.source === 'address') setResolved(null)
})

function confirmDetect() {
  consentOpen.value = false
  detect()
}

function detect() {
  errorMessage.value = ''
  setResolved(null)

  if (!('geolocation' in navigator)) {
    errorMessage.value = t('location_input.errors.unsupported')
    return
  }

  locating.value = true

  navigator.geolocation.getCurrentPosition(
    (position) => {
      locating.value = false
      const value = { latitude: position.coords.latitude, longitude: position.coords.longitude }
      setResolved({
        source: 'geolocation',
        value,
        message: t('location_input.found', { coords: formatCoordinates(value.latitude, value.longitude) }),
      })
    },
    (error) => {
      locating.value = false
      errorMessage.value = t(`location_input.errors.${geolocationFailure(error.code)}`)
    },
  )
}

async function search() {
  if (!searchable.value || searching.value) return

  const run = ++searchRun
  searching.value = true
  errorMessage.value = ''
  setResolved(null)

  try {
    const result = await searchAddress(composeAddress(fields))
    if (run !== searchRun) return

    setResolved({
      source: 'address',
      value: { latitude: result.latitude, longitude: result.longitude },
      message: t('location_input.address_found', { address: result.formattedAddress }),
    })
  }
  catch (err) {
    if (run !== searchRun) return

    errorMessage.value = resolveApiError(err, {
      byStatus: {
        404: t('location_input.errors.address_not_found'),
        502: t('location_input.errors.geocoding'),
        503: t('location_input.errors.geocoding'),
        504: t('location_input.errors.geocoding'),
      },
      fallback: t('location_input.errors.generic'),
      unavailable: t('location_input.errors.unavailable'),
    })
  }
  finally {
    if (run === searchRun) searching.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <UButton
      variant="subtle"
      icon="i-heroicons-map-pin"
      :loading="locating"
      @click="consentOpen = true"
    >
      {{ locating ? $t('location_input.locating') : $t('location_input.detect') }}
    </UButton>

    <p class="text-sm text-muted">
      {{ $t('location_input.or_type') }}
    </p>

    <div class="grid gap-3 sm:grid-cols-12">
      <UFormField :label="$t('location_input.postal_code')" :error="postalCodeError" required class="sm:col-span-4">
        <UInput
          v-model="fields.postalCode"
          :placeholder="$t('location_input.postal_code_placeholder')"
          inputmode="numeric"
          autocomplete="postal-code"
          maxlength="6"
          class="w-full"
          @keydown.enter.prevent="search"
        />
      </UFormField>

      <UFormField :label="$t('location_input.city')" required class="sm:col-span-8">
        <UInput
          v-model="fields.city"
          :placeholder="$t('location_input.city_placeholder')"
          autocomplete="address-level2"
          class="w-full"
          @keydown.enter.prevent="search"
        />
      </UFormField>

      <UFormField :label="$t('location_input.street')" :hint="$t('common.optional')" class="sm:col-span-5">
        <UInput
          v-model="fields.street"
          :placeholder="$t('location_input.street_placeholder')"
          autocomplete="address-line1"
          class="w-full"
          @keydown.enter.prevent="search"
        />
      </UFormField>

      <UFormField :label="$t('location_input.house_number')" :hint="$t('common.optional')" class="sm:col-span-3">
        <UInput
          v-model="fields.houseNumber"
          :placeholder="$t('location_input.house_number_placeholder')"
          class="w-full"
          @keydown.enter.prevent="search"
        />
      </UFormField>

      <div class="flex items-end sm:col-span-4">
        <UButton
          icon="i-heroicons-magnifying-glass"
          class="w-full justify-center"
          :color="searchable ? 'primary' : 'neutral'"
          :variant="searchable ? 'solid' : 'soft'"
          :disabled="!searchable"
          :loading="searching"
          @click="search"
        >
          {{ $t('location_input.search') }}
        </UButton>
      </div>
    </div>

    <p v-if="resolved" class="text-sm text-success" role="status">
      {{ resolved.message }}
    </p>

    <UAlert
      v-if="errorMessage"
      color="error"
      variant="subtle"
      icon="i-heroicons-exclamation-circle"
      :description="errorMessage"
    />

    <UModal
      v-model:open="consentOpen"
      :title="$t('location_input.consent_title')"
      :description="$t('location_input.consent_description')"
    >
      <template #footer>
        <div class="flex justify-end gap-2 w-full">
          <UButton color="neutral" variant="outline" @click="consentOpen = false">
            {{ $t('common.cancel') }}
          </UButton>
          <UButton @click="confirmDetect">
            {{ $t('location_input.consent_allow') }}
          </UButton>
        </div>
      </template>
    </UModal>
  </div>
</template>