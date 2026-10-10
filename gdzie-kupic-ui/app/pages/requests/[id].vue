<script setup lang="ts">
import type { Post } from '~/composables/api/usePostsApi'

definePageMeta({
  layout: 'buyer',
  middleware: ['auth', 'role'],
  roles: ['Buyer'],
  shellTitleKey: 'nav.requests',
})

const { t, locale } = useI18n()
const route = useRoute()
const postsApi = usePostsApi()

const id = computed(() => String(route.params.id))

const { data: post, status: loadState, error: loadError, refresh } = useAsyncData(
  () => `post-${id.value}`,
  () => postsApi.get(id.value),
  { server: false, watch: [id] },
)

useSeoMeta({ title: () => `${post.value?.title ?? t('request.list.title')} | Gdzie Kupić` })

const isLoading = computed(() => (loadState.value === 'idle' || loadState.value === 'pending') && !post.value)
const notFound = computed(() => !post.value && parseApiError(loadError.value).status === 404)
const isActive = computed(() => post.value ? isPostActive(post.value) : true)

// The post itself (status, deadline, long-lived) changes when it ends or is extended: reload it
// when the server says so, not only the status and the responses (see `usePostStatus`).
useRealtimeEvent('postStatusChanged', (event) => {
  if (event.postId === id.value) void refresh()
})
useRealtimeEvent('resync', () => void refresh())

const { status, error: statusError, loaded: statusLoaded, refresh: refreshStatus } = usePostStatus(id, isActive)
// The responses follow the status polling (same schedule, same stop).
const { responses, error: responsesError, loaded: responsesLoaded, refresh: refreshResponses } = usePostResponses(
  id,
  () => (statusLoaded.value ? status.value : undefined),
)

const dateFormat = computed(() => new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }))
const formatDate = (iso: string) => dateFormat.value.format(new Date(iso))

const category = computed(() => post.value ? `${post.value.category.name} · ${post.value.tag.name}` : '')
const coordinates = computed(() => post.value ? `${post.value.latitude.toFixed(4)}, ${post.value.longitude.toFixed(4)}` : '')

const notice = ref('')

// Found / close, each behind a confirmation.
type PostAction = 'fulfil' | 'close'
const pendingAction = ref<PostAction | null>(null)
const acting = ref(false)
const actionError = ref('')

function askConfirmation(action: PostAction) {
  actionError.value = ''
  pendingAction.value = action
}

async function reload() {
  await Promise.all([refresh(), refreshStatus()])
}

async function confirmAction() {
  const action = pendingAction.value
  if (!action) return

  acting.value = true
  actionError.value = ''

  try {
    await postsApi[action](id.value)
    pendingAction.value = null
    notice.value = ''
    await reload()
  }
  catch (err) {
    if (parseApiError(err).status === 409) {
      // Someone (or the expiry job) got there first: show the real state and drop the actions.
      pendingAction.value = null
      notice.value = t('request.detail.conflict')
      await reload()
    }
    else {
      actionError.value = resolveApiError(err, {
        fallback: t('request.detail.action_error'),
        unavailable: t('request.detail.action_unavailable'),
      })
    }
  }
  finally {
    acting.value = false
  }
}

// Zero-match offer: shown once per request; dismissing is remembered locally.
const longLivedDismissed = ref(true) // until the stored choice is read on the client
const longLivedOpen = ref(false)
const extending = ref(false)
const longLivedError = ref('')

const longLivedEligible = computed(() => post.value ? isLongLivedEligible(post.value, status.value) : false)

function readDismissed() {
  longLivedDismissed.value = isLongLivedDismissed(browserStorage(), id.value)
}

onMounted(readDismissed)
watch(id, readDismissed)

watchEffect(() => {
  if (longLivedEligible.value && !longLivedDismissed.value) longLivedOpen.value = true
})

function dismissLongLivedOffer() {
  dismissLongLived(browserStorage(), id.value)
  longLivedDismissed.value = true
  longLivedOpen.value = false
  longLivedError.value = ''
}

async function extend() {
  extending.value = true
  longLivedError.value = ''

  try {
    post.value = await postsApi.makeLongLived(id.value) as Post
    longLivedOpen.value = false
    notice.value = ''
  }
  catch (err) {
    if (parseApiError(err).status === 409) {
      // No longer eligible (e.g. a merchant joined meanwhile): refresh and stop offering.
      longLivedOpen.value = false
      longLivedDismissed.value = true
      notice.value = t('request.detail.long_lived_conflict')
      await reload()
    }
    else {
      longLivedError.value = resolveApiError(err, {
        fallback: t('request.detail.action_error'),
        unavailable: t('request.detail.action_unavailable'),
      })
    }
  }
  finally {
    extending.value = false
  }
}
</script>

<template>
  <div class="container mx-auto max-w-6xl space-y-6 px-4 py-6 lg:py-8">
    <UButton to="/requests" variant="link" color="neutral" icon="i-heroicons-arrow-left" class="px-0">
      {{ $t('request.detail.back') }}
    </UButton>

    <p v-if="isLoading" class="text-sm text-muted">
      {{ $t('common.loading') }}
    </p>

    <UCard v-else-if="notFound">
      <div class="space-y-3 py-8 text-center">
        <UIcon name="i-heroicons-question-mark-circle" class="size-10 text-muted" />
        <p class="font-medium text-highlighted">
          {{ $t('request.detail.not_found_title') }}
        </p>
        <p class="text-sm text-muted">
          {{ $t('request.detail.not_found_text') }}
        </p>
        <UButton to="/requests">
          {{ $t('request.detail.back') }}
        </UButton>
      </div>
    </UCard>

    <div v-else-if="!post" class="space-y-3">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="$t('request.detail.load_error')"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="refresh()">
        {{ $t('common.retry') }}
      </UButton>
    </div>

    <template v-else>
      <UAlert
        v-if="notice"
        color="warning"
        variant="subtle"
        icon="i-heroicons-information-circle"
        :description="notice"
        data-testid="request-notice"
      />

      <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="min-w-0 space-y-2">
          <h1 class="text-2xl font-bold text-highlighted">
            {{ post.title }}
          </h1>
          <RequestBadges :post="post" />
        </div>

        <div v-if="isPostActive(post)" class="flex flex-wrap gap-2">
          <UButton icon="i-heroicons-check-circle" @click="askConfirmation('fulfil')">
            {{ $t('request.detail.found') }}
          </UButton>
          <UButton color="neutral" variant="outline" icon="i-heroicons-x-circle" @click="askConfirmation('close')">
            {{ $t('request.detail.close') }}
          </UButton>
        </div>
      </div>

      <div class="grid gap-6 lg:grid-cols-3">
        <UCard class="lg:col-span-2">
          <template #header>
            <h2 class="text-base font-semibold text-highlighted">
              {{ $t('request.detail.details') }}
            </h2>
          </template>

          <p v-if="post.description" class="mb-4 whitespace-pre-line text-sm">
            {{ post.description }}
          </p>

          <dl class="grid gap-3 text-sm sm:grid-cols-2">
            <div>
              <dt class="text-muted">{{ $t('request.category') }}</dt>
              <dd class="font-medium text-highlighted">{{ category }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('request.radius_km') }}</dt>
              <dd class="font-medium text-highlighted">
                {{ post.radiusKm === null ? $t('request.form.radius_unlimited') : $t('request.form.radius_km_value', { km: post.radiusKm }) }}
              </dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('request.location') }}</dt>
              <dd class="font-medium text-highlighted">{{ coordinates }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('request.detail.created') }}</dt>
              <dd class="font-medium text-highlighted">{{ formatDate(post.createdAt) }}</dd>
            </div>
            <div>
              <dt class="text-muted">
                {{ isPostActive(post) ? $t('request.detail.expires') : $t('request.detail.ended') }}
              </dt>
              <dd class="font-medium text-highlighted" data-testid="post-expires">
                {{ formatDate(post.expiresAt) }}
              </dd>
            </div>
            <div v-if="isPostActive(post)">
              <dt class="text-muted">{{ $t('request.detail.time_left') }}</dt>
              <dd class="font-medium text-highlighted"><RequestRemaining :expires-at="post.expiresAt" /></dd>
            </div>
          </dl>
        </UCard>

        <RequestStatusPanel
          :post="post"
          :status="status"
          :loaded="statusLoaded"
          :failed="statusError !== null"
          @retry="refreshStatus()"
        />
      </div>

      <RequestResponses
        :responses="responses"
        :loaded="responsesLoaded"
        :failed="responsesError !== null"
        @retry="refreshResponses()"
      />
    </template>

    <RequestConfirmDialog
      :open="pendingAction !== null"
      :title="$t(`request.detail.confirm_${pendingAction ?? 'close'}_title`)"
      :description="$t(`request.detail.confirm_${pendingAction ?? 'close'}_text`)"
      :confirm-label="$t(`request.detail.confirm_${pendingAction ?? 'close'}_button`)"
      :busy="acting"
      :error="actionError"
      @confirm="confirmAction"
      @cancel="pendingAction = null"
    />

    <RequestLongLivedDialog
      :open="longLivedOpen"
      :busy="extending"
      :error="longLivedError"
      @accept="extend"
      @dismiss="dismissLongLivedOffer"
    />
  </div>
</template>
