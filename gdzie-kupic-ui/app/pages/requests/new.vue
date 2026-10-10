<script setup lang="ts">
import {
  DESCRIPTION_MAX_LENGTH,
  TITLE_MAX_LENGTH,
  URGENT_MAX_HOURS,
  URGENT_MIN_HOURS,
} from '~/utils/postForm'

definePageMeta({
  layout: 'buyer',
  middleware: ['auth', 'role'],
  roles: ['Buyer'],
  shellTitleKey: 'request.new',
})

const { t } = useI18n()
useSeoMeta({ title: () => `${t('request.new')} | Gdzie Kupić` })

const postsApi = usePostsApi()
const catalogueApi = useCatalogueApi()
const savedLocationsApi = useSavedLocationsApi()

const {
  data: categories,
  status: categoriesStatus,
  error: categoriesError,
  refresh: refreshCategories,
} = useAsyncData('post-form-categories', () => catalogueApi.getCategories(), {
  server: false,
  default: () => [] as CatalogueCategory[],
})

const { data: savedLocations, status: savedStatus, error: savedError } = useAsyncData(
  'post-form-saved-locations',
  () => savedLocationsApi.list(),
  { server: false, default: () => [] as SavedLocation[] },
)

const form = reactive<PostFormState>(emptyPostForm())

// The limits are shown in the template and error messages.
const limits = {
  title: TITLE_MAX_LENGTH,
  description: DESCRIPTION_MAX_LENGTH,
  minHours: URGENT_MIN_HOURS,
  maxHours: URGENT_MAX_HOURS,
}

// ─── Catalogue selectors (disabled entries are not offered) ─────────────────
const categoryItems = computed(() =>
  enabledCategories(categories.value).map(category => ({ label: category.name, value: category.id })),
)
const tagItems = computed(() =>
  enabledTags(categories.value, form.categoryId).map(tag => ({ label: tag.name, value: tag.id })),
)

function onCategoryChange(categoryId: string | undefined) {
  form.categoryId = categoryId ?? ''
  form.tagId = ''
}

// ─── Urgency ────────────────────────────────────────────────────────────────
const bounds = ref(deadlineBounds(new Date()))

watch(() => form.urgent, (urgent) => {
  if (urgent) bounds.value = deadlineBounds(new Date())
  else form.deadline = ''
})

// ─── Validation ─────────────────────────────────────────────────────────────
// Errors appear after the first submit attempt and then follow the form live.
const attempted = ref(false)

const errors = computed(() =>
  attempted.value ? validatePostForm(form, categories.value, new Date()) : [],
)

function errorFor(field: PostFormField): string | undefined {
  const error = errors.value.find(candidate => candidate.name === field)
  if (!error) return undefined

  return t(`request.form.errors.${error.code}`, {
    max: field === 'title' ? limits.title : limits.description,
    minHours: limits.minHours,
    maxHours: limits.maxHours,
  })
}

// ─── Submit ─────────────────────────────────────────────────────────────────
const submitting = ref(false)
const submitError = ref('')
const createdId = ref<string | null>(null)
const savedFromForm = ref<LocationInputValue | null>(null)
const saveDialogOpen = ref(false)

async function submit() {
  attempted.value = true
  submitError.value = ''
  if (validatePostForm(form, categories.value, new Date()).length > 0) return

  submitting.value = true

  try {
    const created = await postsApi.create(buildCreatePostRequest(form))
    createdId.value = created.id

    if (form.location?.source === 'form') {
      savedFromForm.value = { latitude: form.location.latitude, longitude: form.location.longitude }
      saveDialogOpen.value = true
    }
    else {
      await openCreated()
    }
  }
  catch (err) {
    submitError.value = resolveApiError(err, {
      fallback: t('request.form.errors.generic'),
      unavailable: t('request.form.errors.unavailable'),
    })
  }
  finally {
    submitting.value = false
  }
}

async function openCreated() {
  if (createdId.value) await navigateTo(`/requests/${createdId.value}`)
}
</script>

<template>
  <div class="container mx-auto px-4 py-10 max-w-2xl">
    <header class="mb-8">
      <h1 class="text-2xl font-bold">
        {{ $t('request.new') }}
      </h1>
      <p class="text-sm text-muted mt-1">
        {{ $t('request.form.description') }}
      </p>
    </header>

    <UForm :state="form" class="space-y-6" novalidate @submit="submit">
      <UFormField :label="$t('request.title')" name="title" :error="errorFor('title')" required>
        <UInput
          v-model="form.title"
          :placeholder="$t('request.form.title_placeholder')"
          :maxlength="limits.title"
          class="w-full"
        />
      </UFormField>

      <UFormField
        :label="$t('request.description')"
        :hint="$t('common.optional')"
        name="description"
        :error="errorFor('description')"
      >
        <UTextarea
          v-model="form.description"
          :rows="4"
          :maxlength="limits.description"
          :placeholder="$t('request.form.description_placeholder')"
          class="w-full"
        />
      </UFormField>

      <div class="space-y-3">
        <UAlert
          v-if="categoriesError"
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="$t('request.form.catalogue_error')"
        >
          <template #actions>
            <UButton size="xs" variant="outline" color="error" @click="refreshCategories()">
              {{ $t('common.retry') }}
            </UButton>
          </template>
        </UAlert>

        <div class="grid gap-4 sm:grid-cols-2">
          <UFormField :label="$t('request.category')" name="category" :error="errorFor('category')" required>
            <USelect
              :model-value="form.categoryId || undefined"
              :items="categoryItems"
              :loading="categoriesStatus === 'pending'"
              :placeholder="$t('request.form.category_placeholder')"
              class="w-full"
              @update:model-value="onCategoryChange"
            />
          </UFormField>

          <UFormField :label="$t('request.form.tag')" name="tag" :error="errorFor('tag')" required>
            <USelect
              :model-value="form.tagId || undefined"
              :items="tagItems"
              :disabled="!form.categoryId"
              :placeholder="$t('request.form.tag_placeholder')"
              class="w-full"
              @update:model-value="(value: string | undefined) => (form.tagId = value ?? '')"
            />
          </UFormField>
        </div>
      </div>

      <UFormField :label="$t('request.location')" name="location" :error="errorFor('location')" required>
        <RequestPostLocationField
          v-model="form.location"
          :saved="savedLocations"
          :loading="savedStatus === 'pending' || savedStatus === 'idle'"
          :load-failed="!!savedError"
        />
      </UFormField>

      <UFormField :label="$t('request.form.radius_label')" name="radius" :error="errorFor('radius')" required>
        <RequestRadiusField
          v-model:mode="form.radiusMode"
          v-model:custom="form.customRadius"
          :error="errorFor('radius')"
        />
      </UFormField>

      <div class="space-y-3">
        <USwitch
          v-model="form.urgent"
          :label="$t('request.form.urgent')"
          :description="$t('request.form.urgent_hint', { minHours: limits.minHours, maxHours: limits.maxHours })"
        />

        <UFormField
          v-if="form.urgent"
          :label="$t('request.form.deadline')"
          name="deadline"
          :error="errorFor('deadline')"
          required
        >
          <UInput
            v-model="form.deadline"
            type="datetime-local"
            :min="bounds.min"
            :max="bounds.max"
            class="w-full sm:w-64"
          />
        </UFormField>
      </div>

      <UAlert
        v-if="submitError"
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="submitError"
      />

      <UButton type="submit" size="lg" class="w-full justify-center" :loading="submitting">
        {{ $t('request.submit') }}
      </UButton>
    </UForm>

    <RequestSaveLocationDialog
      v-model:open="saveDialogOpen"
      :location="savedFromForm"
      @done="openCreated"
    />
  </div>
</template>
