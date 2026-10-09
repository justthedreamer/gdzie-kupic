/** Value held by `LocationInput` — either browser coordinates or a typed address. */
export type LocationInputValue =
  | { kind: 'coords', latitude: number, longitude: number }
  | { kind: 'address', address: string }

/** Location part of the API request bodies — exactly one method is sent. */
export type LocationPayload =
  | { latitude: number, longitude: number }
  | { address: string }

export function toLocationPayload(value: LocationInputValue | null): LocationPayload | null {
  if (!value) return null

  if (value.kind === 'coords') {
    return { latitude: value.latitude, longitude: value.longitude }
  }

  const address = value.address.trim()
  return address ? { address } : null
}

export type GeolocationFailure = 'denied' | 'unavailable' | 'timeout' | 'unknown'

// GeolocationPositionError codes: 1 = PERMISSION_DENIED, 2 = POSITION_UNAVAILABLE, 3 = TIMEOUT
export function geolocationFailure(code: number): GeolocationFailure {
  switch (code) {
    case 1: return 'denied'
    case 2: return 'unavailable'
    case 3: return 'timeout'
    default: return 'unknown'
  }
}

export function formatCoordinates(latitude: number, longitude: number): string {
  return `${latitude.toFixed(5)}, ${longitude.toFixed(5)}`
}
