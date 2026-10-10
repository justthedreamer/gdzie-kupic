import { describe, it, expect } from 'vitest'
import { reconnectDelay, realtimeHubUrl, SERVER_EVENTS } from '~/utils/realtime'

describe('realtimeHubUrl', () => {
  it('appends the hub path to the API base', () => {
    expect(realtimeHubUrl('http://localhost:5000')).toBe('http://localhost:5000/hubs/app')
  })

  it('ignores trailing slashes of the API base', () => {
    expect(realtimeHubUrl('https://api.example.com/')).toBe('https://api.example.com/hubs/app')
  })
})

describe('reconnectDelay', () => {
  it('backs off from an immediate retry up to 30 seconds', () => {
    expect([0, 1, 2, 3, 4, 5].map(reconnectDelay)).toEqual([0, 1000, 2000, 5000, 10_000, 30_000])
  })

  it('stays at 30 seconds for ever', () => {
    expect(reconnectDelay(6)).toBe(30_000)
    expect(reconnectDelay(500)).toBe(30_000)
  })

  it('treats a negative count as the first retry', () => {
    expect(reconnectDelay(-1)).toBe(0)
  })
})

describe('SERVER_EVENTS', () => {
  it('lists the six events of the contract and not the local resync', () => {
    expect([...SERVER_EVENTS].sort()).toEqual([
      'messageReceived',
      'notificationRaised',
      'postAdded',
      'postRemoved',
      'postStatusChanged',
      'threadUpdated',
    ])
  })
})
