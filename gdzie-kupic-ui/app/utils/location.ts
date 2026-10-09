/** Value held by `LocationInput` — resolved coordinates (detected by the browser or found by address search). */
export interface LocationInputValue {
  latitude: number
  longitude: number
}

/** Location part of the API request bodies. */
export type LocationPayload = LocationInputValue

export function toLocationPayload(value: LocationInputValue | null): LocationPayload | null {
  return value ? { latitude: value.latitude, longitude: value.longitude } : null
}

export interface AddressFields {
  postalCode: string
  city: string
  street: string
  houseNumber: string
}

const POSTAL_CODE_PATTERN = /^\d{2}-\d{3}$/

export function isValidPostalCode(postalCode: string): boolean {
  return POSTAL_CODE_PATTERN.test(postalCode.trim())
}

/** Postal code and city are required; street and house number are optional. */
export function isAddressSearchable(fields: AddressFields): boolean {
  return isValidPostalCode(fields.postalCode) && fields.city.trim().length > 0
}

/** Builds a single geocodable string, e.g. `Rynek Główny 1, 31-042 Kraków, Polska`. */
export function composeAddress(fields: AddressFields): string {
  const street = fields.street.trim()
  const streetLine = street ? [street, fields.houseNumber.trim()].filter(Boolean).join(' ') : ''
  const cityLine = [fields.postalCode.trim(), fields.city.trim()].filter(Boolean).join(' ')

  return [streetLine, cityLine, 'Polska'].filter(Boolean).join(', ')
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