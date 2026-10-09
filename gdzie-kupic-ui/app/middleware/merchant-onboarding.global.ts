// A Merchant who has not onboarded yet is sent into the onboarding flow from
// any page. Onboarded merchants (and every other role) are never redirected.
export default defineNuxtRouteMiddleware(async (to) => {
  // Auth state lives client-side only, so there is nothing to check on the server.
  if (import.meta.server) return

  const authStore = useAuthStore()
  if (!authStore.isMerchant) return

  if (to.path === '/merchant/onboarding' || to.path.startsWith('/auth')) return

  try {
    const profile = await useMerchantStore().load()
    if (!profile) return navigateTo('/merchant/onboarding')
  }
  catch {
    // A failing lookup must not lock the merchant out of the app.
  }
})
