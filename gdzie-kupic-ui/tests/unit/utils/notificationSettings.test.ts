import { describe, it, expect } from 'vitest'
import { pushBannerMode, pushSwitchView } from '~/utils/notificationSettings'

describe('pushSwitchView', () => {
  it('is an enabled, checked switch when push is on', () => {
    expect(pushSwitchView('on')).toEqual({ checked: true, disabled: false, hint: 'on' })
  })

  it('is an enabled, unchecked switch when push is off', () => {
    expect(pushSwitchView('off')).toEqual({ checked: false, disabled: false, hint: 'off' })
  })

  it.each([
    ['unsupported', 'unsupported'],
    ['notConfigured', 'not_configured'],
    ['blocked', 'blocked'],
    ['unknown', 'loading'],
  ] as const)('is disabled and explains why when the state is %s', (state, hint) => {
    expect(pushSwitchView(state)).toEqual({ checked: false, disabled: true, hint })
  })
})

describe('pushBannerMode', () => {
  it('invites to turn push on while it is off and undecided', () => {
    expect(pushBannerMode('off', 'default', false)).toBe('enable')
  })

  it('hints at "add to home screen" where the browser cannot do push', () => {
    expect(pushBannerMode('unsupported', 'default', false)).toBe('install')
  })

  it('is hidden once dismissed, whatever the state', () => {
    expect(pushBannerMode('off', 'default', true)).toBeNull()
    expect(pushBannerMode('unsupported', 'default', true)).toBeNull()
  })

  it.each(['on', 'blocked', 'notConfigured', 'unknown'] as const)('is hidden when push is %s', (state) => {
    expect(pushBannerMode(state, 'default', false)).toBeNull()
  })

  it('is hidden when the permission was already decided', () => {
    expect(pushBannerMode('off', 'granted', false)).toBeNull()
    expect(pushBannerMode('off', 'denied', false)).toBeNull()
  })
})
