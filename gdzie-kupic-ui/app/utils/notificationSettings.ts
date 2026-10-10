import type { PushState } from '~/utils/push'

// Pure rules of the notification settings screen and of the push banner
// (planning/phase-7-push-notifications.md, decisions E3 and E4).

export const NOTIFICATION_SETTINGS_PATH = '/settings/notifications'

/** localStorage flag: the user dismissed the banner on this device. */
export const PUSH_BANNER_DISMISSED_KEY = 'gk:push-banner-dismissed'

export interface PushSwitchView {
  checked: boolean
  disabled: boolean
  /** Suffix of the `notification_settings.push.*` i18n key that explains the state. */
  hint: 'loading' | 'unsupported' | 'not_configured' | 'blocked' | 'off' | 'on'
}

/** How the push switch looks for the state of push on this device. */
export function pushSwitchView(state: PushState | 'unknown'): PushSwitchView {
  switch (state) {
    case 'on':
      return { checked: true, disabled: false, hint: 'on' }
    case 'off':
      return { checked: false, disabled: false, hint: 'off' }
    case 'unsupported':
      return { checked: false, disabled: true, hint: 'unsupported' }
    case 'notConfigured':
      return { checked: false, disabled: true, hint: 'not_configured' }
    case 'blocked':
      return { checked: false, disabled: true, hint: 'blocked' }
    default:
      return { checked: false, disabled: true, hint: 'loading' }
  }
}

/** `enable`: invite to turn push on; `install`: the browser cannot do push, hint at "add to home screen". */
export type PushBannerMode = 'enable' | 'install'

/**
 * What the banner on the buyer home / the merchant feed shows, or `null` for nothing: never after
 * it was dismissed, and never when push is already on, blocked, not configured or not read yet.
 */
export function pushBannerMode(
  state: PushState | 'unknown',
  permission: NotificationPermission,
  dismissed: boolean,
): PushBannerMode | null {
  if (dismissed) return null
  if (state === 'unsupported') return 'install'
  if (state === 'off' && permission === 'default') return 'enable'

  return null
}
