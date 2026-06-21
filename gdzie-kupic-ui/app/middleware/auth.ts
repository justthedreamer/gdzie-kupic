/**
 * Protect routes that require authentication.
 * Apply via definePageMeta({ middleware: 'auth' }) on a page.
 */
export default defineNuxtRouteMiddleware(() => {
  const authStore = useAuthStore()

  if (!authStore.isAuthenticated) {
    return navigateTo('/auth/login')
  }
})
