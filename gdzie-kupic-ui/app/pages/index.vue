<script setup lang="ts">
import type { PostListItem } from '~/composables/api/usePostsApi'

const { t } = useI18n()

useSeoMeta({
  title: 'Gdzie Kupić',
  description: t('home.tagline'),
})

const demo = {
  description: null,
  latitude: 50.06,
  longitude: 19.94,
  status: 'Active',
  notificationDispatchStatus: 'Dispatched',
  isUrgent: false,
  urgentDeadline: null,
  isLongLived: false,
} as const

const hoursFromNow = (h: number) => new Date(Date.now() + h * 3600 * 1000).toISOString()

const recentRequests: PostListItem[] = [
  {
    ...demo,
    id: '1',
    title: 'Szukam mikrofonu Shure SM7B',
    category: { id: 'c1', name: 'Elektronika' },
    tag: { id: 't1', name: 'Mikrofony' },
    radiusKm: 20,
    notifiedCount: 14,
    expiresAt: hoursFromNow(70),
    createdAt: hoursFromNow(-1),
  },
  {
    ...demo,
    id: '2',
    title: 'Potrzebuję roweru górskiego dla dziecka 24"',
    category: { id: 'c2', name: 'Sport' },
    tag: { id: 't2', name: 'Rowery' },
    radiusKm: 10,
    notifiedCount: 8,
    expiresAt: hoursFromNow(46),
    createdAt: hoursFromNow(-2),
  },
  {
    ...demo,
    id: '3',
    title: 'Szukam używanego iPhone 14 Pro',
    category: { id: 'c3', name: 'Telefony' },
    tag: { id: 't3', name: 'Smartfony' },
    radiusKm: 15,
    notifiedCount: 22,
    expiresAt: hoursFromNow(5),
    createdAt: hoursFromNow(-3),
  },
]</script>

<template>
  <div>
  <!-- Hero -->
  <AppHero
    :title="$t('home.hero_title')"
    :subtitle="$t('home.tagline')"
    :cta-label="$t('home.cta')"
    cta-to="/requests/new"
  />

  <!-- How it works -->
  <section id="how-it-works" class="py-16 bg-default">
    <div class="container mx-auto px-4 max-w-4xl">
      <h2 class="text-2xl font-bold text-center mb-10 text-highlighted">
        {{ $t('home.how_it_works') }}
      </h2>
      <div class="grid md:grid-cols-3 gap-8 text-center">
        <div v-for="(step, i) in [$t('home.step_1'), $t('home.step_2'), $t('home.step_3')]" :key="i">
          <div class="mx-auto mb-3 size-10 rounded-full bg-primary/10 text-primary font-bold flex items-center justify-center">
            {{ i + 1 }}
          </div>
          <p class="text-sm text-muted">{{ step }}</p>
        </div>
      </div>
    </div>
  </section>

  <!-- Recent requests -->
  <section class="py-16 bg-muted">
    <div class="container mx-auto px-4 max-w-4xl">
      <div class="flex items-center justify-between mb-8">
        <h2 class="text-2xl font-bold text-highlighted">Ostatnie zapytania</h2>
        <UButton to="/requests" variant="ghost">
          Zobacz wszystkie →
        </UButton>
      </div>
      <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <RequestCard
          v-for="req in recentRequests"
          :key="req.id"
          :post="req"
        />
      </div>
    </div>
  </section>
  </div>
</template>
