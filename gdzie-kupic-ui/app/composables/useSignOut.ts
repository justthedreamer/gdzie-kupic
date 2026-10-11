/**
 * Signs the user out. The push registration of this device is removed first (it needs the
 * user's token) so the next user of the device never gets someone else's notifications.
 * Never fails and never waits long.
 */
export function useSignOut() {
  const auth = useAuthStore()
  const push = usePushStore()

  return async () => {
    await push.removeOnSignOut()
    auth.clearAuth()
  }
}
