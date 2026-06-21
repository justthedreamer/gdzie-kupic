<script setup lang="ts">
definePageMeta({ layout: 'auth' })
useSeoMeta({ title: 'Logowanie | Gdzie Kupić' })

const form = reactive({ email: '', password: '' })
const error = ref('')

async function submit() {
  error.value = ''
  try {
    const api = useApi()
    const res = await api.post<{ token: string; user: { id: string; email: string; role: 'Buyer' | 'Merchant' | 'Admin' } }>(
      '/api/auth/login',
      form,
    )
    useAuthStore().setAuth(res.token, res.user)
    await navigateTo('/')
  }
  catch {
    error.value = 'Nieprawidłowy e-mail lub hasło'
  }
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 px-4">
    <div class="w-full max-w-sm">
      <div class="mb-8 text-center">
        <NuxtLink to="/" class="text-2xl font-bold">Gdzie Kupić</NuxtLink>
        <p class="mt-1 text-sm text-gray-500">Zaloguj się do swojego konta</p>
      </div>

      <UCard>
        <UForm :state="form" class="space-y-4" @submit="submit">
          <UFormField :label="$t('auth.email')" name="email">
            <UInput v-model="form.email" type="email" autocomplete="email" />
          </UFormField>

          <UFormField :label="$t('auth.password')" name="password">
            <UInput v-model="form.password" type="password" autocomplete="current-password" />
          </UFormField>

          <UAlert v-if="error" color="error" variant="soft" :description="error" />

          <UButton type="submit" class="w-full">
            {{ $t('auth.login') }}
          </UButton>
        </UForm>

        <div class="mt-4 text-center text-sm text-gray-500">
          Nie masz konta?
          <NuxtLink to="/auth/register" class="font-medium text-primary-600 hover:underline">
            {{ $t('auth.register') }}
          </NuxtLink>
        </div>
      </UCard>
    </div>
  </div>
</template>
