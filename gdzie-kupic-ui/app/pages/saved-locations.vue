<script setup lang="ts">
definePageMeta({ middleware: ['auth', 'role'], roles: ['Buyer'] })

const { t } = useI18n()
useSeoMeta({ title: () => `${t('saved_locations.title')} | Gdzie Kupić` })

const savedLocationsApi = useSavedLocationsApi()

const { data: locations, status, error: loadError, refresh } = useAsyncData(
  'saved-locations',
  () => savedLocationsApi.list(),
  { server: false, default: () => [] as SavedLocation[] },
)
const isLoading = computed(() => (status.value === 'idle' || status.value === 'pending') && !locations.value.length)

// ─── Add ────────────────────────────────────────────────────────────────────
const displayName = ref('')
const location = ref<LocationInputValue | null>(null)
const locationKey = ref(0)
const saving = ref(false)
const formError = ref('')

const canSave = computed(() =>
  displayName.value.trim().length > 0 && toLocationPayload(location.value) !== null,
)

async function save() {
  const payload = toLocationPayload(location.value)
  if (!canSave.value || !payload) return

  saving.value = true
  formError.value = ''

  try {
    await savedLocationsApi.create({ displayName: displayName.value.trim(), ...payload })
    displayName.value = ''
    location.value = null
    locationKey.value++
    await refresh()
  }
  catch (err) {
    formError.value = resolveApiError(err, {
      byStatus: {
        502: t('saved_locations.errors.geocoding'),
        503: t('saved_locations.errors.geocoding'),
        504: t('saved_locations.errors.geocoding'),
      },
      fallback: t('saved_locations.errors.generic'),
      unavailable: t('saved_locations.errors.unavailable'),
    })
  }
  finally {
    saving.value = false
  }
}

// ─── Delete ─────────────────────────────────────────────────────────────────
const pendingDelete = ref<SavedLocation | null>(null)
const deleteOpen = computed({
  get: () => pendingDelete.value !== null,
  set: (open: boolean) => {
    if (!open) pendingDelete.value = null
  },
})
const deleting = ref(false)
const listError = ref('')

async function confirmDelete() {
  const target = pendingDelete.value
  if (!target) return

  deleting.value = true
  listError.value = ''

  try {
    await savedLocationsApi.remove(target.id)
  }
  catch (err) {
    listError.value = resolveApiError(err, {
      byStatus: { 404: t('saved_locations.errors.not_found') },
      fallback: t('saved_locations.errors.generic'),
      unavailable: t('saved_locations.errors.unavailable'),
    })
  }
  finally {
    deleting.value = false
    pendingDelete.value = null
  }

  // Refresh also after a 404 so the list matches the server again.
  await refresh()
}
</script>

<template>
  <div class="container mx-auto px-4 py-10 max-w-2xl space-y-8">
    <header>
      <h1 class="text-2xl font-bold">
        {{ $t('saved_locations.title') }}
      </h1>
      <p class="text-sm text-muted mt-1">
        {{ $t('saved_locations.description') }}
      </p>
    </header>

    <UCard>
      <template #header>
        <h2 class="text-base font-semibold">
          {{ $t('saved_locations.add_title') }}
        </h2>
      </template>

      <UForm :state="{ displayName, location }" class="space-y-5" @submit="save">
        <UFormField :label="$t('saved_locations.name_label')" name="displayName">
          <UInput
            v-model="displayName"
            :placeholder="$t('saved_locations.name_placeholder')"
            maxlength="100"
            class="w-full"
          />
        </UFormField>

        <UFormField :label="$t('saved_locations.location_label')" name="location">
          <LocationInput :key="locationKey" v-model="location" />
        </UFormField>

        <UAlert
          v-if="formError"
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="formError"
        />

        <UButton type="submit" :loading="saving" :disabled="!canSave">
          {{ $t('saved_locations.save') }}
        </UButton>
      </UForm>
    </UCard>

    <section class="space-y-3">
      <UAlert
        v-if="listError"
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="listError"
      />

      <p v-if="isLoading" class="text-sm text-muted">
        {{ $t('common.loading') }}
      </p>

      <div v-else-if="loadError" class="space-y-3">
        <UAlert
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="$t('saved_locations.load_error')"
        />
        <UButton variant="outline" icon="i-heroicons-arrow-path" @click="refresh()">
          {{ $t('common.retry') }}
        </UButton>
      </div>

      <p v-else-if="!locations.length" class="text-center py-10 text-muted">
        {{ $t('saved_locations.empty') }}
      </p>

      <ul v-else class="space-y-2">
        <li
          v-for="item in locations"
          :key="item.id"
          class="flex items-center justify-between gap-4 rounded-lg border border-default bg-default p-4"
        >
          <div class="min-w-0">
            <p class="font-medium truncate">
              {{ item.displayName }}
            </p>
            <p class="text-sm text-muted">
              {{ formatCoordinates(item.latitude, item.longitude) }}
            </p>
          </div>
          <UButton
            color="error"
            variant="ghost"
            icon="i-heroicons-trash"
            :aria-label="`${$t('common.delete')}: ${item.displayName}`"
            @click="pendingDelete = item"
          />
        </li>
      </ul>
    </section>

    <UModal
      v-model:open="deleteOpen"
      :title="$t('saved_locations.delete_title')"
      :description="$t('saved_locations.delete_confirm', { name: pendingDelete?.displayName ?? '' })"
    >
      <template #footer>
        <div class="flex justify-end gap-2 w-full">
          <UButton color="neutral" variant="outline" :disabled="deleting" @click="pendingDelete = null">
            {{ $t('common.cancel') }}
          </UButton>
          <UButton color="error" :loading="deleting" @click="confirmDelete">
            {{ $t('common.delete') }}
          </UButton>
        </div>
      </template>
    </UModal>
  </div>
</template>
