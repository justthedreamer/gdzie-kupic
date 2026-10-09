export type UserRole = 'Buyer' | 'Merchant' | 'Admin'

interface User {
  id: string
  email: string
  role: UserRole
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const token = ref<string | null>(null)

  const isAuthenticated = computed(() => !!token.value)
  const isMerchant = computed(() => user.value?.role === 'Merchant')
  const isAdmin = computed(() => user.value?.role === 'Admin')

  function setAuth(newToken: string, newUser: User) {
    token.value = newToken
    user.value = newUser
  }

  function clearAuth() {
    token.value = null
    user.value = null
  }

  return {
    user,
    token,
    isAuthenticated,
    isMerchant,
    isAdmin,
    setAuth,
    clearAuth,
  }
})
