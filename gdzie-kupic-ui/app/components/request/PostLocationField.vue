<script setup lang="ts">
// Location of the request: the buyer's saved locations next to the location form (detect / address).
// Only one source is active at a time — choosing one clears the other.
const location = defineModel<PostLocation | null>({ required: true })

const props = defineProps<{
  saved: SavedLocation[]
  loading?: boolean
  loadFailed?: boolean
}>()

const formKey = ref(0)

const selectedSavedId = computed({
  get: () => (location.value?.source === 'saved' ? (location.value.savedId ?? undefined) : undefined),
  set: (id: string | undefined) => {
    const chosen = props.saved.find(item => item.id === id)
    if (!chosen) return

    // Reset the form so it does not keep showing a result that is no longer used.
    formKey.value++
    location.value = { latitude: chosen.latitude, longitude: chosen.longitude, source: 'saved', savedId: chosen.id }
  },
})

const savedItems = computed(() =>
  props.saved.map(item => ({
    label: item.displayName,
    description: item.addressDisplayName || formatCoordinates(item.latitude, item.longitude),
    value: item.id,
  })),
)

function onFormLocation(value: LocationInputValue | null) {
  if (value) {
    location.value = { latitude: value.latitude, longitude: value.longitude, source: 'form', savedId: null }
  }
  else if (location.value?.source === 'form') {
    location.value = null
  }
}
</script>

<template>
  <div class="space-y-5">
    <section class="space-y-2">
      <h3 class="text-sm font-medium text-highlighted">
        {{ $t('request.form.saved_locations') }}
      </h3>

      <p v-if="loading" class="text-sm text-muted">
        {{ $t('common.loading') }}
      </p>
      <p v-else-if="loadFailed" class="text-sm text-muted">
        {{ $t('request.form.saved_locations_error') }}
      </p>
      <p v-else-if="!saved.length" class="text-sm text-muted">
        {{ $t('request.form.saved_locations_empty') }}
      </p>
      <URadioGroup
        v-else
        v-model="selectedSavedId"
        :items="savedItems"
        :legend="$t('request.form.saved_locations')"
        :ui="{ legend: 'sr-only', fieldset: 'flex-col gap-y-2' }"
      />
    </section>

    <section class="space-y-2">
      <h3 class="text-sm font-medium text-highlighted">
        {{ $t('request.form.other_location') }}
      </h3>
      <LocationInput :key="formKey" :model-value="location?.source === 'form' ? location : null" @update:model-value="onFormLocation" />
    </section>
  </div>
</template>
