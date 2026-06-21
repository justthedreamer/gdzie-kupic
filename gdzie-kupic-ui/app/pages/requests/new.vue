<script setup lang="ts">
definePageMeta({ middleware: 'auth' })
useSeoMeta({ title: 'Nowe zapytanie | Gdzie Kupić' })

const { t } = useI18n()

const form = reactive({
  title: '',
  description: '',
  category: '',
  city: '',
  radiusKm: 10,
})

async function submit() {
  const api = useApi()
  await api.post('/api/requests', form)
  await navigateTo('/requests')
}
</script>

<template>
  <div class="container mx-auto px-4 py-10 max-w-xl">
    <h1 class="text-2xl font-bold mb-8">
      {{ $t('request.new') }}
    </h1>

    <UForm :state="form" class="space-y-5" @submit="submit">
      <UFormField :label="$t('request.title')" name="title">
        <UInput v-model="form.title" :placeholder="$t('request.title')" />
      </UFormField>

      <UFormField :label="$t('request.description')" name="description">
        <UTextarea v-model="form.description" :rows="4" />
      </UFormField>

      <UFormField :label="$t('request.category')" name="category">
        <UInput v-model="form.category" />
      </UFormField>

      <div class="grid grid-cols-2 gap-4">
        <UFormField :label="$t('request.location')" name="city">
          <UInput v-model="form.city" />
        </UFormField>
        <UFormField :label="$t('request.radius_km')" name="radiusKm">
          <UInput v-model.number="form.radiusKm" type="number" min="1" max="100" />
        </UFormField>
      </div>

      <UButton type="submit" class="w-full">
        {{ $t('request.submit') }}
      </UButton>
    </UForm>
  </div>
</template>
