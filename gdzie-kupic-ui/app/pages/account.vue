<script setup lang="ts">
// One page for every role, inside the shell of the signed-in role (the inline
// middleware picks the layout; it cannot be declared outside definePageMeta).
definePageMeta({
  layout: 'buyer',
  middleware: [
    'auth',
    'role',
    () => {
      const role = useAuthStore().user?.role
      if (role === 'Merchant') setPageLayout('merchant')
      else if (role === 'Admin') setPageLayout('admin')
      else if (role === 'Buyer') setPageLayout('buyer')
    },
  ],
  roles: ['Buyer', 'Merchant', 'Admin'],
  shellTitleKey: 'account.title',
})

const { t } = useI18n()
useSeoMeta({ title: () => `${t('account.title')} | Gdzie Kupić` })

const authStore = useAuthStore()
const profileStore = useProfileStore()

const loading = ref(true)
const loadFailed = ref(false)
const firstName = ref('')
const saving = ref(false)
const saved = ref(false)
const saveError = ref('')

async function load() {
  loading.value = true
  loadFailed.value = false

  try {
    const profile = await useAccountApi().get()
    firstName.value = profile.firstName ?? ''
    profileStore.set(profile.firstName)
  }
  catch {
    loadFailed.value = true
  }
  finally {
    loading.value = false
  }
}

onMounted(load)

const problem = computed(() => firstNameProblem(firstName.value))
const problemText = computed(() => {
  if (problem.value === 'too_long') return t('account.first_name_too_long', { max: FIRST_NAME_MAX_LENGTH })
  if (problem.value === 'invalid') return t('account.first_name_invalid')
  return ''
})

const unchanged = computed(() => normalizeFirstName(firstName.value) === (profileStore.firstName ?? ''))
const canSave = computed(() => !loading.value && !saving.value && problem.value === null && !unchanged.value)

watch(firstName, (value) => {
  saveError.value = ''
  // Storing the normalized name rewrites the field; that is not an edit.
  if (normalizeFirstName(value) !== (profileStore.firstName ?? '')) saved.value = false
})

async function save() {
  if (!canSave.value) return

  saving.value = true
  saved.value = false
  saveError.value = ''

  try {
    firstName.value = (await profileStore.save(firstName.value)) ?? ''
    saved.value = true
  }
  catch (err) {
    saveError.value = resolveApiError(err, {
      byStatus: { 400: t('account.first_name_invalid') },
      fallback: t('account.save_error'),
      unavailable: t('account.save_error'),
    })
  }
  finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="container mx-auto max-w-2xl space-y-6 px-4 py-6 lg:py-8">
    <h1 class="text-2xl font-bold text-highlighted">
      {{ $t('account.title') }}
    </h1>

    <p v-if="loading" class="text-sm text-muted">
      {{ $t('common.loading') }}
    </p>

    <div v-else-if="loadFailed" class="space-y-3" data-testid="account-load-error">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="$t('account.load_error')"
      />
      <UButton variant="outline" @click="load">
        {{ $t('common.retry') }}
      </UButton>
    </div>

    <UCard v-else>
      <form class="space-y-5" novalidate @submit.prevent="save">
        <UFormField :label="$t('account.email')" :help="$t('account.email_hint')">
          <UInput
            :model-value="authStore.user?.email ?? ''"
            type="email"
            disabled
            class="w-full"
            data-testid="account-email"
          />
        </UFormField>

        <UFormField
          :label="$t('account.first_name')"
          :help="$t('account.first_name_hint')"
          :error="problemText || undefined"
        >
          <UInput
            v-model="firstName"
            :maxlength="FIRST_NAME_MAX_LENGTH + 20"
            autocomplete="given-name"
            :placeholder="$t('account.first_name_placeholder')"
            class="w-full"
            data-testid="account-first-name"
          />
        </UFormField>

        <UAlert
          v-if="saveError"
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="saveError"
          role="alert"
          data-testid="account-save-error"
        />

        <p v-if="saved" class="text-sm text-success" role="status" data-testid="account-saved">
          {{ $t('account.saved') }}
        </p>

        <UButton type="submit" :disabled="!canSave" :loading="saving" data-testid="account-save">
          {{ $t('common.save') }}
        </UButton>
      </form>
    </UCard>
  </div>
</template>
