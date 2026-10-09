// /api/location — reverse geocoding (coordinates → address) and address search (address → coordinates)
export interface LocationRequest {
  latitude: string
  longitude: string
}

export interface LocationResponse {
  voivodeship: string
  postalCode: string
  city: string
  country: string
}

export interface AddressSearchResponse {
  latitude: number
  longitude: number
  formattedAddress: string
}

export const useLocationApi = () => {
  const api = useApi()

  return {
    getLocation: (coords: LocationRequest): Promise<LocationResponse> =>
      api.get<LocationResponse>('/api/location', { params: coords }),

    /** Resolves a typed address to coordinates without saving anything. */
    searchAddress: (address: string): Promise<AddressSearchResponse> =>
      api.get<AddressSearchResponse>('/api/location/search', { params: { address } }),
  }
}