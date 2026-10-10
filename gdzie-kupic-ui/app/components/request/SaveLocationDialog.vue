<script setup lang="ts">
// Shown after a request was created with a location typed into the form: offers to save it under a name.
// Declining or a failed save never blocks the flow — the parent just continues to the new request.
const open = defineModel<boolean>('open', { required: true })

const props = defineProps<{
  location: LocationInputValue | null
}>()

const emit = defineEmits<{
  done: []
}>()

const { t } = useI18n()
const savedLocationsApi = useSavedLocationsApi()

const displayName = ref('')
const saving = ref(false)
const errorMessage = ref('')

const canSave = computed(() => displayName.value.trim().length > 0 && props.location !== null)

watch(open, (isOpen) => {
  if (isOpen) {
    displayName.value = ''
    errorMessage.value = ''
  }
})

function close() {
  open.value = false
  emit('done')
}

async function save() {
  const payload = toLocationPayload(props.location)
  if (!canSave.value || !payload) return

  saving.value = true
  errorMessage.value = ''

  try {
    await savedLocationsApi.create({ displayName: displayName.value.trim(), ...payload })
    close()
  }
  catch (err) {
    errorMessage.value = resolveApiError(err, {
      fallback: t('saved_locations.errors.generic'),
      unavailable: t('saved_locations.errors.unavailable'),
    })
  }
  finally {
    saving.value = false
  }
}
</script>

<template>
  <UModal
    v-model:open="open"
    :dismissible="false"
    :title="$t('request.form.save_location_title')"
    :description="$t('request.form.save_location_description')"
    :close="false"
  >
    <template #body>
      <div class="space-y-4">
        <UFormField :label="$t('saved_locations.name_label')">
          <UInput
            v-model="displayName"
            :placeholder="$t('saved_locations.name_placeholder')"
            maxlength="100"
            class="w-full"
            @keydown.enter.prevent="save"
          />
        </UFormField>

        <UAlert
          v-if="errorMessage"
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="errorMessage"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2 w-full">
        <UButton color="neutral" variant="outline" :disabled="saving" @click="close">
          {{ errorMessage ? $t('common.close') : $t('request.form.save_location_skip') }}
        </UButton>
        <UButton :loading="saving" :disabled="!canSave" @click="save">
          {{ $t('saved_locations.save') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
