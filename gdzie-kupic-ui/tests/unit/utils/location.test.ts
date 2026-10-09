import { describe, it, expect } from 'vitest'
import {
  composeAddress,
  formatCoordinates,
  geolocationFailure,
  isAddressSearchable,
  isValidPostalCode,
  toLocationPayload,
} from '~/utils/location'

const fields = { postalCode: '31-042', city: 'Kraków', street: '', houseNumber: '' }

describe('toLocationPayload', () => {
  it('returns null when nothing is resolved', () => {
    expect(toLocationPayload(null)).toBeNull()
  })

  it('sends only the coordinates', () => {
    expect(toLocationPayload({ latitude: 50.06, longitude: 19.94 }))
      .toEqual({ latitude: 50.06, longitude: 19.94 })
  })
})

describe('isValidPostalCode', () => {
  it('accepts the Polish NN-NNN format', () => {
    expect(isValidPostalCode('31-042')).toBe(true)
    expect(isValidPostalCode(' 00-001 ')).toBe(true)
  })

  it('rejects anything else', () => {
    expect(isValidPostalCode('')).toBe(false)
    expect(isValidPostalCode('31042')).toBe(false)
    expect(isValidPostalCode('31-04')).toBe(false)
    expect(isValidPostalCode('ab-cde')).toBe(false)
  })
})

describe('isAddressSearchable', () => {
  it('needs only a valid postal code and a city', () => {
    expect(isAddressSearchable(fields)).toBe(true)
  })

  it('is not searchable without a city or with a bad postal code', () => {
    expect(isAddressSearchable({ ...fields, city: '  ' })).toBe(false)
    expect(isAddressSearchable({ ...fields, postalCode: '310' })).toBe(false)
    expect(isAddressSearchable({ ...fields, postalCode: '' })).toBe(false)
  })

  it('does not depend on the optional fields', () => {
    expect(isAddressSearchable({ ...fields, street: 'Rynek Główny', houseNumber: '1' })).toBe(true)
  })
})

describe('composeAddress', () => {
  it('builds postal code, city and country when only the required fields are given', () => {
    expect(composeAddress(fields)).toBe('31-042 Kraków, Polska')
  })

  it('adds the street, and the house number after it', () => {
    expect(composeAddress({ ...fields, street: ' Rynek Główny ', houseNumber: ' 1 ' }))
      .toBe('Rynek Główny 1, 31-042 Kraków, Polska')
    expect(composeAddress({ ...fields, street: 'Rynek Główny' }))
      .toBe('Rynek Główny, 31-042 Kraków, Polska')
  })

  it('ignores a house number given without a street', () => {
    expect(composeAddress({ ...fields, houseNumber: '1' })).toBe('31-042 Kraków, Polska')
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