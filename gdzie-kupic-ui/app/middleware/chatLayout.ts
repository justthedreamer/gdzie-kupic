// The chat pages are the same for a buyer and a merchant, but live inside the shell of
// the signed-in role. Use after `auth` and `role`:
//   definePageMeta({ middleware: ['auth', 'role', 'chat-layout'], roles: ['Buyer', 'Merchant'] })
export default defineNuxtRouteMiddleware(() => {
  const role = useAuthStore().user?.role
  if (role === 'Merchant') setPageLayout('merchant')
  else if (role === 'Buyer') setPageLayout('buyer')
})
