<script setup lang="ts">
const { t } = useI18n()

useSeoMeta({
  title: 'Gdzie Kupić',
  description: t('home.tagline'),
})

// Dummy data — replace with a real API call via useApi()
const recentRequests = [
  {
    id: '1',
    title: 'Szukam mikrofonu Shure SM7B',
    category: 'Elektronika',
    city: 'Kraków',
    radiusKm: 20,
    notifiedCount: 14,
    offersCount: 3,
    postedAt: new Date(Date.now() - 3600 * 1000),
  },
  {
    id: '2',
    title: 'Potrzebuję roweru górskiego dla dziecka 24"',
    category: 'Sport',
    city: 'Warszawa',
    radiusKm: 10,
    notifiedCount: 8,
    offersCount: 1,
    postedAt: new Date(Date.now() - 7200 * 1000),
  },
  {
    id: '3',
    title: 'Szukam używanego iPhone 14 Pro',
    category: 'Telefony',
    city: 'Wrocław',
    radiusKm: 15,
    notifiedCount: 22,
    offersCount: 5,
    postedAt: new Date(Date.now() - 10800 * 1000),
  },
]
</script>

<template>
  <!-- Hero -->
  <AppHero
    :title="$t('home.hero_title')"
    :subtitle="$t('home.tagline')"
    :cta-label="$t('home.cta')"
    cta-to="/requests/new"
  />

  <!-- How it works -->
  <section id="how-it-works" class="py-16 bg-white">
    <div class="container mx-auto px-4 max-w-4xl">
      <h2 class="text-2xl font-bold text-center mb-10">
        {{ $t('home.how_it_works') }}
      </h2>
      <div class="grid md:grid-cols-3 gap-8 text-center">
        <div v-for="(step, i) in [$t('home.step_1'), $t('home.step_2'), $t('home.step_3')]" :key="i">
          <div class="mx-auto mb-3 size-10 rounded-full bg-primary-100 text-primary-700 font-bold flex items-center justify-center">
            {{ i + 1 }}
          </div>
          <p class="text-sm text-gray-600">{{ step }}</p>
        </div>
      </div>
    </div>
  </section>

  <!-- Recent requests -->
  <section class="py-16 bg-gray-50">
    <div class="container mx-auto px-4 max-w-4xl">
      <div class="flex items-center justify-between mb-8">
        <h2 class="text-2xl font-bold">Ostatnie zapytania</h2>
        <UButton to="/requests" variant="ghost">
          Zobacz wszystkie →
        </UButton>
      </div>
      <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <RequestCard
          v-for="req in recentRequests"
          :key="req.id"
          v-bind="req"
        />
      </div>
    </div>
  </section>
</template>
