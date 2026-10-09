// Navigation of the Buyer app shell (sidebar on desktop, bottom tabs + overflow
// menu on mobile). An item without `to` has no page yet and is rendered disabled.

declare module '#app' {
  interface PageMeta {
    /** i18n key of the title shown in the mobile top bar of the buyer layout. */
    buyerTitleKey?: string
  }
}

export interface BuyerNavItem {
  key: string
  icon: string
  /** i18n key. */
  label: string
  to?: string
}

export const BUYER_NEW_REQUEST_PATH = '/requests/new'

const home: BuyerNavItem = { key: 'home', icon: 'i-heroicons-home', label: 'buyer_nav.home', to: '/home' }
const requests: BuyerNavItem = { key: 'requests', icon: 'i-heroicons-clipboard-document-list', label: 'nav.requests', to: '/requests' }
const chats: BuyerNavItem = { key: 'chats', icon: 'i-heroicons-chat-bubble-left-right', label: 'buyer_nav.chats' }
const savedSearches: BuyerNavItem = { key: 'saved_searches', icon: 'i-heroicons-bookmark', label: 'buyer_nav.saved_searches' }
const savedLocations: BuyerNavItem = { key: 'saved_locations', icon: 'i-heroicons-map-pin', label: 'nav.saved_locations', to: '/saved-locations' }
const profile: BuyerNavItem = { key: 'profile', icon: 'i-heroicons-user', label: 'buyer_nav.profile' }
const settings: BuyerNavItem = { key: 'settings', icon: 'i-heroicons-cog-6-tooth', label: 'buyer_nav.settings' }

/** Desktop sidebar. */
export const buyerSidebarNav: BuyerNavItem[] = [home, requests, chats, savedSearches, savedLocations, profile, settings]

/** Mobile bottom tab bar, left and right of the centre "new request" action. */
export const buyerTabsLeft: BuyerNavItem[] = [home, requests]
export const buyerTabsRight: BuyerNavItem[] = [chats, profile]

/** Mobile overflow menu — everything that has no tab. */
export const buyerOverflowNav: BuyerNavItem[] = [savedLocations, savedSearches, settings]
