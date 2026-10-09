import { describe, it, expect } from 'vitest'
import { formatCoordinates, geolocationFailure, toLocationPayload } from '~/utils/location'

describe('toLocationPayload', () => {
  it('returns null when nothing is selected', () => {
    expect(toLocationPayload(null)).toBeNull()
  })

  it('sends only coordinates for the "find me" method', () => {
    expect(toLocationPayload({ kind: 'coords', latitude: 50.06, longitude: 19.94 }))
      .toEqual({ latitude: 50.06, longitude: 19.94 })
  })

  it('sends only the trimmed address for the typed method', () => {
    expect(toLocationPayload({ kind: 'address', address: '  Rynek 1, Kraków ' }))
      .toEqual({ address: 'Rynek 1, Kraków' })
  })

  it('treats a blank address as no location', () => {
    expect(toLocationPayload({ kind: 'address', address: '   ' })).toBeNull()
  })
})

describe('geolocationFailure', () => {
  it('maps GeolocationPositionError codes', () => {
    expect(geolocationFailure(1)).toBe('denied')
    expect(geolocationFailure(2)).toBe('unavailable')
    expect(geolocationFailure(3)).toBe('timeout')
    expect(geolocationFailure(99)).toBe('unknown')
  })
})

describe('formatCoordinates', () => {
  it('rounds to five decimals', () => {
    expect(formatCoordinates(50.064702, 19.945)).toBe('50.06470, 19.94500')
  })
})
