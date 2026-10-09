<script setup lang="ts">
definePageMeta({ middleware: ['auth', 'role'], roles: ['Merchant'] })

const { t } = useI18n()
useSeoMeta({ title: () => `${t('merchant.onboarding.title')} | Gdzie Kupić` })

const merchantApi = useMerchantApi()
const catalogueApi = useCatalogueApi()
const merchantStore = useMerchantStore()

const TOTAL_STEPS = 2
const step = ref(1)

// An onboarded merchant is not forced through the flow again.
onMounted(async () => {
  try {
    if (await merchantStore.load()) await navigateTo('/merchant/subscriptions')
  }
  catch {
    // Lookup failure: let the merchant continue; submitting reports real errors.
  }
})

// ─── Step 1: business & branch ──────────────────────────────────────────────
const form = reactive({
  name: '',
  description: '',
  branchName: '',
  phone: '',
  website: '',
})
const location = ref<LocationInputValue | null>(null)

const locationPayload = computed(() => toLocationPayload(location.value))
const businessValid = computed(() =>
  form.name.trim().length > 0 && form.branchName.trim().length > 0 && locationPayload.value !== null,
)

// ─── Step 2: subscriptions ──────────────────────────────────────────────────
const { data: categories, status: categoriesStatus, error: categoriesError, refresh: refreshCategories } = useAsyncData(
  'onboarding-catalogue',
  () => catalogueApi.getCategories(),
  { server: false, default: () => [] as CatalogueCategory[] },
)
const categoriesLoading = computed(() =>
  categoriesStatus.value === 'idle' || categoriesStatus.value === 'pending',
)
const selection = ref<SubscriptionTarget[]>([])

// ─── Submit ─────────────────────────────────────────────────────────────────
const submitting = ref(false)
const submitError = ref('')
// Set once the merchant has been created, so a retry never re-posts it.
const createdProfile = ref<MerchantProfile | null>(null)

function goToStep(next: number) {
  step.value = next
  submitError.value = ''
}

async function createMerchant(payload: LocationPayload): Promise<boolean> {
  try {
    createdProfile.value = await merchantApi.onboard({
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      branch: {
        displayName: form.branchName.trim(),
        phone: form.phone.trim() || undefined,
        website: form.website.trim() || undefined,
        ...payload,
      },
    })
    merchantStore.setProfile(createdProfile.value)
    return true
  }
  catch (err) {
    // 409: the merchant already exists (e.g. created in another tab) — carry on.
    if (parseApiError(err).status === 409) {
      createdProfile.value = await merchantStore.load(true).catch(() => null)
      if (createdProfile.value) return true
    }

    submitError.value = resolveApiError(err, {
      byStatus: {
        502: t('merchant.onboarding.errors.geocoding'),
        503: t('merchant.onboarding.errors.geocoding'),
        504: t('merchant.onboarding.errors.geocoding'),
      },
      fallback: t('merchant.onboarding.errors.onboarding'),
      unavailable: t('merchant.onboarding.errors.unavailable'),
    })
    return false
  }
}

async function finish() {
  const payload = locationPayload.value
  if (!businessValid.value || !payload) {
    goToStep(1)
    submitError.value = t('merchant.onboarding.errors.validation')
    return
  }

  submitting.value = true
  submitError.value = ''

  try {
    if (!createdProfile.value && !(await createMerchant(payload))) return

    // Keep only the failures selected, so a retry resubmits just those.
    const failed = await merchantApi.subscribeMany(selection.value)
    selection.value = failed

    if (failed.length) {
      submitError.value = t('merchant.onboarding.errors.subscriptions')
      return
    }

    await navigateTo('/merchant/subscriptions')
  }
  finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="container mx-auto px-4 py-10 max-w-2xl space-y-8">
    <header>
      <h1 class="text-2xl font-bold">
        {{ $t('merchant.onboarding.title') }}
      </h1>
      <p class="text-sm text-muted mt-1">
        {{ $t('merchant.onboarding.description') }}
      </p>
    </header>

    <div>
      <p class="text-sm font-medium" data-testid="onboarding-step">
        {{ $t('merchant.onboarding.step_of', { current: step, total: TOTAL_STEPS }) }}
        —
        {{ step === 1 ? $t('merchant.onboarding.step_business') : $t('merchant.onboarding.step_subscriptions') }}
      </p>
      <div class="mt-2 h-1.5 rounded-full bg-elevated" aria-hidden="true">
        <div
          class="h-full rounded-full bg-primary transition-all"
          :style="{ width: `${(step / TOTAL_STEPS) * 100}%` }"
        />
      </div>
    </div>

    <UAlert
      v-if="submitError"
      color="error"
      variant="subtle"
      icon="i-heroicons-exclamation-circle"
      :description="submitError"
    />

    <!-- Step 1 (kept mounted so the location input keeps its state when going back) -->
    <UForm v-show="step === 1" :state="form" class="space-y-5" @submit="goToStep(2)">
      <UFormField :label="$t('merchant.onboarding.business_name')" name="name" required>
        <UInput v-model="form.name" maxlength="150" class="w-full" />
      </UFormField>

      <UFormField
        :label="$t('merchant.onboarding.business_description')"
        :hint="$t('common.optional')"
        name="description"
      >
        <UTextarea v-model="form.description" :rows="3" maxlength="1000" class="w-full" />
      </UFormField>

      <UFormField :label="$t('merchant.onboarding.branch_name')" name="branchName" required>
        <UInput
          v-model="form.branchName"
          :placeholder="$t('merchant.onboarding.branch_name_placeholder')"
          maxlength="150"
          class="w-full"
        />
      </UFormField>

      <div class="grid sm:grid-cols-2 gap-4">
        <UFormField :label="$t('merchant.onboarding.phone')" :hint="$t('common.optional')" name="phone">
          <UInput v-model="form.phone" type="tel" maxlength="30" class="w-full" />
        </UFormField>
        <UFormField :label="$t('merchant.onboarding.website')" :hint="$t('common.optional')" name="website">
          <UInput v-model="form.website" type="url" placeholder="https://" maxlength="200" class="w-full" />
        </UFormField>
      </div>

      <UFormField :label="$t('merchant.onboarding.branch_location')" name="location" required>
        <LocationInput v-model="location" />
      </UFormField>

      <UButton type="submit" :disabled="!businessValid">
        {{ $t('common.next') }}
      </UButton>
    </UForm>

    <!-- Step 2 -->
    <div v-if="step === 2" class="space-y-5">
      <p class="text-sm text-muted">
        {{ $t('merchant.onboarding.subscriptions_hint') }}
      </p>

      <p v-if="categoriesLoading" class="text-sm text-muted">
        {{ $t('common.loading') }}
      </p>

      <div v-else-if="categoriesError" class="space-y-3">
        <UAlert
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="$t('merchant.onboarding.categories_load_error')"
        />
        <UButton variant="outline" icon="i-heroicons-arrow-path" @click="refreshCategories()">
          {{ $t('common.retry') }}
        </UButton>
      </div>

      <p v-else-if="!categories.length" class="text-muted">
        {{ $t('merchant.onboarding.no_categories') }}
      </p>

      <SubscriptionPicker v-else v-model="selection" :categories="categories" />

      <div class="flex gap-2">
        <UButton
          v-if="!createdProfile"
          color="neutral"
          variant="outline"
          :disabled="submitting"
          @click="goToStep(1)"
        >
          {{ $t('common.back') }}
        </UButton>
        <UButton :loading="submitting" @click="finish">
          {{ submitError ? $t('common.retry') : $t('merchant.onboarding.finish') }}
        </UButton>
      </div>
    </div>
  </div>
</template>
