<script setup lang="ts">
definePageMeta({ layout: 'auth' })
useSeoMeta({ title: 'Rejestracja | Gdzie Kupić' })

const { t } = useI18n()
const form = reactive({ email: '', password: '', firstName: '' })
const error = ref('')

const firstNameError = computed(() => {
  const problem = firstNameProblem(form.firstName)
  if (problem === 'too_long') return t('account.first_name_too_long', { max: FIRST_NAME_MAX_LENGTH })
  if (problem === 'invalid') return t('account.first_name_invalid')
  return undefined
})

async function submit() {
  error.value = ''
  if (firstNameError.value) return

  try {
    const api = useApi()
    const res = await api.post<{ token: string; user: { id: string; email: string; role: 'Buyer' | 'Merchant' | 'Admin' } }>(
      '/api/auth/register',
      { email: form.email, password: form.password, firstName: normalizeFirstName(form.firstName) || null },
    )
    useAuthStore().setAuth(res.token, res.user)
    await navigateTo(homePathFor(res.user.role))
  }
  catch {
    error.value = 'Rejestracja nie powiodła się. Spróbuj ponownie.'
  }
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 px-4">
    <div class="w-full max-w-sm">
      <div class="mb-8 text-center">
        <NuxtLink to="/" class="text-2xl font-bold">Gdzie Kupić</NuxtLink>
        <p class="mt-1 text-sm text-gray-500">Utwórz nowe konto</p>
      </div>

      <UCard>
        <UForm :state="form" class="space-y-4" @submit="submit">
          <UFormField :label="`${$t('auth.first_name')} (${$t('common.optional')})`" name="firstName" :error="firstNameError">
            <UInput v-model="form.firstName" autocomplete="given-name" class="w-full" data-testid="register-first-name" />
          </UFormField>

          <UFormField :label="$t('auth.email')" name="email">
            <UInput v-model="form.email" type="email" autocomplete="email" class="w-full" />
          </UFormField>

          <UFormField :label="$t('auth.password')" name="password">
            <UInput v-model="form.password" type="password" autocomplete="new-password" class="w-full" />
          </UFormField>

          <UAlert v-if="error" color="error" variant="soft" :description="error" />

          <UButton type="submit" class="w-full">
            {{ $t('auth.register') }}
          </UButton>
        </UForm>

        <div class="mt-4 text-center text-sm text-gray-500">
          Masz już konto?
          <NuxtLink to="/auth/login" class="font-medium text-primary-600 hover:underline">
            {{ $t('auth.login') }}
          </NuxtLink>
        </div>
      </UCard>
    </div>
  </div>
</template>
