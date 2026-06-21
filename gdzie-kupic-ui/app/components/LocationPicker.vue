<script setup lang="ts">
import { useLocationApi, type LocationResponse } from '~/composables/api/useLocationApi'

const emit = defineEmits<{
  confirm: [location: LocationResponse]
}>()

type State = 'idle' | 'locating' | 'fetching' | 'success' | 'error'

const state = ref<State>('idle')
const errorMessage = ref('')
const location = ref<LocationResponse | null>(null)

const { getLocation } = useLocationApi()

const isLoading = computed(() => state.value === 'locating' || state.value === 'fetching')

const loadingLabel = computed(() =>
  state.value === 'locating' ? 'Pobieranie lokalizacji…' : 'Weryfikacja adresu…',
)

async function detect() {
  if (!navigator.geolocation) {
    state.value = 'error'
    errorMessage.value = 'Twoja przeglądarka nie obsługuje geolokalizacji.'
    return
  }

  state.value = 'locating'
  errorMessage.value = ''
  location.value = null

  try {
    const coords = await new Promise<GeolocationCoordinates>((resolve, reject) =>
      navigator.geolocation.getCurrentPosition(
        (pos) => resolve(pos.coords),
        (err) => reject(err),
      ),
    )

    state.value = 'fetching'

    location.value = await getLocation({
      latitude: String(coords.latitude),
      longitude: String(coords.longitude),
    })

    state.value = 'success'
  }
  catch (err: unknown) {
    state.value = 'error'

    if (err instanceof GeolocationPositionError) {
      const messages: Record<number, string> = {
        [GeolocationPositionError.PERMISSION_DENIED]: 'Odmówiłeś dostępu do lokalizacji.',
        [GeolocationPositionError.POSITION_UNAVAILABLE]: 'Nie można określić Twojej lokalizacji.',
        [GeolocationPositionError.TIMEOUT]: 'Upłynął limit czasu pobierania lokalizacji.',
      }
      errorMessage.value = messages[err.code] ?? 'Nieznany błąd geolokalizacji.'
    }
    else if (err && typeof err === 'object' && 'data' in err) {
      // ofetch throws FetchError — .data contains ProblemDetails from the backend
      const problem = (err as { data?: { title?: string; detail?: string } }).data
      const message = problem?.detail ?? problem?.title
      errorMessage.value = message?.trim()
        ? message
        : 'Nie udało się pobrać lokalizacji, wystąpił nieoczekiwany błąd.'
    }
    else {
      // No response received (network error, timeout, server unreachable)
      errorMessage.value = 'Nie udało się pobrać lokalizacji. Serwis jest w tej chwili niedostępny, spróbuj ponownie później.'
    }
  }
}

function confirm() {
  if (location.value) emit('confirm', location.value)
}

function reset() {
  state.value = 'idle'
  errorMessage.value = ''
  location.value = null
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <!-- Idle / trigger -->
    <UButton
      v-if="state === 'idle'"
      icon="i-heroicons-map-pin-solid"
      @click="detect"
    >
      Wykryj moją lokalizację
    </UButton>

    <!-- Loading -->
    <div
      v-else-if="isLoading"
      class="flex items-center gap-2 text-sm text-gray-500"
    >
      <UIcon name="i-heroicons-arrow-path" class="size-4 animate-spin" />
      {{ loadingLabel }}
    </div>

    <!-- Error -->
    <template v-else-if="state === 'error'">
      <UAlert
        color="error"
        icon="i-heroicons-exclamation-circle"
        title="Nie udało się pobrać lokalizacji"
        :description="errorMessage"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="detect">
        Spróbuj ponownie
      </UButton>
    </template>

    <!-- Success -->
    <template v-else-if="state === 'success' && location">
      <UAlert
        color="success"
        icon="i-heroicons-check-circle"
        title="Znaleziono lokalizację"
        description="Czy to Twoja lokalizacja?"
      />

      <div class="rounded-lg border bg-white p-4 grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
        <div class="text-gray-500">Miasto</div>
        <div class="font-medium">{{ location.city }}</div>

        <div class="text-gray-500">Kod pocztowy</div>
        <div class="font-medium">{{ location.postalCode }}</div>

        <div class="text-gray-500">Województwo</div>
        <div class="font-medium">{{ location.voivodeship }}</div>

        <div class="text-gray-500">Kraj</div>
        <div class="font-medium">{{ location.country }}</div>
      </div>

      <div class="flex gap-2">
        <UButton icon="i-heroicons-check" @click="confirm">
          Potwierdź
        </UButton>
        <UButton variant="ghost" icon="i-heroicons-arrow-path" @click="reset">
          Zmień
        </UButton>
      </div>
    </template>
  </div>
</template>
