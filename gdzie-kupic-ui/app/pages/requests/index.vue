<script setup lang="ts">
import type { PostScope } from '~/composables/api/usePostsApi'

definePageMeta({
  layout: 'buyer',
  middleware: ['auth', 'role'],
  roles: ['Buyer'],
  shellTitleKey: 'nav.requests',
})

const { t } = useI18n()
useSeoMeta({ title: () => `${t('request.list.title')} | Gdzie Kupić` })

const postsApi = usePostsApi()

const tab = ref<PostScope>('active')
const tabItems = computed(() => [
  { label: t('request.list.tab_active'), value: 'active' },
  { label: t('request.list.tab_ended'), value: 'ended' },
])

const { data, status, error, refresh } = useAsyncData(
  () => `requests-${tab.value}`,
  () => postsApi.list(tab.value),
  { server: false, watch: [tab], default: () => [] },
)

const isLoading = computed(() => status.value === 'idle' || status.value === 'pending')
</script>

<template>
  <div class="container mx-auto max-w-4xl space-y-6 px-4 py-6 lg:py-8">
    <div class="flex items-center justify-between gap-4">
      <h1 class="text-2xl font-bold text-highlighted">
        {{ $t('request.list.title') }}
      </h1>
      <UButton :to="BUYER_NEW_REQUEST_PATH" icon="i-heroicons-plus">
        {{ $t('request.new') }}
      </UButton>
    </div>

    <UTabs
      v-model="tab"
      :items="tabItems"
      :content="false"
      variant="link"
      :aria-label="$t('request.list.title')"
    />

    <p v-if="isLoading" class="text-sm text-muted">
      {{ $t('common.loading') }}
    </p>

    <div v-else-if="error" class="space-y-3">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="$t('request.list.load_error')"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="refresh()">
        {{ $t('common.retry') }}
      </UButton>
    </div>

    <ul v-else-if="data.length" class="grid gap-4 sm:grid-cols-2">
      <li v-for="post in data" :key="post.id">
        <RequestCard :post="post" />
      </li>
    </ul>

    <UCard v-else>
      <div class="space-y-3 py-8 text-center">
        <UIcon name="i-heroicons-magnifying-glass" class="size-10 text-muted" />
        <p class="font-medium text-highlighted">
          {{ tab === 'active' ? $t('request.list.empty_active_title') : $t('request.list.empty_ended_title') }}
        </p>
        <p class="text-sm text-muted">
          {{ tab === 'active' ? $t('request.list.empty_active_text') : $t('request.list.empty_ended_text') }}
        </p>
        <UButton v-if="tab === 'active'" :to="BUYER_NEW_REQUEST_PATH" icon="i-heroicons-plus">
          {{ $t('request.new') }}
        </UButton>
      </div>
    </UCard>
  </div>
</template>
