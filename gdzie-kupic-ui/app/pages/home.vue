<script setup lang="ts">
definePageMeta({
  layout: 'buyer',
  middleware: ['auth', 'role'],
  roles: ['Buyer'],
  buyerTitleKey: 'buyer_home.mobile_title',
})

const { t } = useI18n()
useSeoMeta({ title: () => `${t('buyer_home.title')} | Gdzie Kupić` })

const authStore = useAuthStore()
const buyerHomeApi = useBuyerHomeApi()

const { data, status, error, refresh } = useAsyncData(
  'buyer-home',
  () => buyerHomeApi.load(),
  { server: false, default: () => emptyBuyerHome() },
)
const isLoading = computed(() => (status.value === 'idle' || status.value === 'pending') && !data.value.requests.length)

const requests = computed(() => sortNewestFirst(data.value.requests))

const selectedId = ref<string | null>(null)
const selected = computed(() =>
  requests.value.find(request => request.id === selectedId.value) ?? pickDefaultRequest(requests.value),
)

const activity = computed(() =>
  data.value.activity
    .filter(event => event.requestId === selected.value?.id)
    .sort((a, b) => Date.parse(b.occurredAt) - Date.parse(a.occurredAt))
    .slice(0, 5),
)
const chats = computed(() => data.value.chats.slice(0, 4))

const name = computed(() => displayNameFromEmail(authStore.user?.email))
</script>

<template>
  <div class="container mx-auto max-w-6xl space-y-6 px-4 py-6 lg:py-8">
    <div class="hidden items-start justify-between gap-4 lg:flex">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">
          {{ $t('buyer_home.title') }}
        </h1>
        <p class="mt-1 text-sm text-muted">
          {{ $t('buyer_home.welcome', { name }) }}
        </p>
      </div>
      <UButton :to="BUYER_NEW_REQUEST_PATH" icon="i-heroicons-plus">
        {{ $t('buyer_home.new_request') }}
      </UButton>
    </div>

    <p v-if="isLoading" class="text-sm text-muted">
      {{ $t('common.loading') }}
    </p>

    <div v-else-if="error" class="space-y-3">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="$t('buyer_home.load_error')"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="refresh()">
        {{ $t('common.retry') }}
      </UButton>
    </div>

    <UCard v-else-if="!selected">
      <div class="space-y-3 py-8 text-center">
        <UIcon name="i-heroicons-magnifying-glass" class="size-10 text-muted" />
        <p class="font-medium text-highlighted">
          {{ $t('buyer_home.empty_title') }}
        </p>
        <p class="text-sm text-muted">
          {{ $t('buyer_home.empty_text') }}
        </p>
        <UButton :to="BUYER_NEW_REQUEST_PATH" icon="i-heroicons-plus">
          {{ $t('buyer_home.new_request') }}
        </UButton>
      </div>
    </UCard>

    <template v-else>
      <BuyerActiveRequestsStrip
        :requests="requests"
        :selected-id="selected.id"
        @select="selectedId = $event"
      />

      <div class="grid gap-6 lg:grid-cols-3">
        <BuyerRequestSummary :request="selected" class="lg:col-span-2" />
        <BuyerLiveStatus :request="selected" />
      </div>

      <div class="grid gap-6 lg:grid-cols-2">
        <BuyerRecentActivity :events="activity" />
        <BuyerRecentChats :chats="chats" class="hidden lg:block" />
      </div>
    </template>
  </div>
</template>
