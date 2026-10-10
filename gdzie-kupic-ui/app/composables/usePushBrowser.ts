import type { PushBrowser } from '~/utils/pushBrowser'

/** The browser's push API (or the `pushMock` fake), installed by plugins/push.client.ts. */
export const usePushBrowser = (): PushBrowser => useNuxtApp().$pushBrowser as PushBrowser
