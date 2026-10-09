// Navigation of the role-specific app shells (sidebar on desktop, bottom tabs +
// overflow menu on mobile). An item without `to` has no page yet and is
// rendered disabled.

declare module '#app' {
  interface PageMeta {
    /** i18n key of the title shown in the mobile top bar of a shell layout. */
    shellTitleKey?: string
  }
}

export interface ShellNavItem {
  key: string
  icon: string
  /** i18n key. */
  label: string
  to?: string
}

export interface ShellConfig {
  /** i18n key of the role label under the logo. */
  roleLabel: string
  /** The shell's home page: the mobile top bar shows no back arrow there. */
  homePath: string
  /** Desktop sidebar. */
  sidebar: ShellNavItem[]
  /** Mobile bottom tab bar, left and right of the optional centre action. */
  tabsLeft: ShellNavItem[]
  tabsRight: ShellNavItem[]
  /** Mobile overflow menu — everything that has no tab. */
  overflow: ShellNavItem[]
  /** Round primary action in the middle of the bottom tab bar. */
  centerAction?: { to: string, label: string }
}

export const BUYER_NEW_REQUEST_PATH = '/requests/new'

// ─── Buyer ──────────────────────────────────────────────────────────────────
const buyerHome: ShellNavItem = { key: 'home', icon: 'i-heroicons-home', label: 'buyer_nav.home', to: '/home' }
const buyerRequests: ShellNavItem = { key: 'requests', icon: 'i-heroicons-clipboard-document-list', label: 'nav.requests', to: '/requests' }
const buyerChats: ShellNavItem = { key: 'chats', icon: 'i-heroicons-chat-bubble-left-right', label: 'buyer_nav.chats' }
const savedSearches: ShellNavItem = { key: 'saved_searches', icon: 'i-heroicons-bookmark', label: 'buyer_nav.saved_searches' }
const savedLocations: ShellNavItem = { key: 'saved_locations', icon: 'i-heroicons-map-pin', label: 'nav.saved_locations', to: '/saved-locations' }
const buyerProfile: ShellNavItem = { key: 'profile', icon: 'i-heroicons-user', label: 'buyer_nav.profile' }
const buyerSettings: ShellNavItem = { key: 'settings', icon: 'i-heroicons-cog-6-tooth', label: 'buyer_nav.settings' }

export const buyerShell: ShellConfig = {
  roleLabel: 'buyer_nav.role_label',
  homePath: '/home',
  sidebar: [buyerHome, buyerRequests, buyerChats, savedSearches, savedLocations, buyerProfile, buyerSettings],
  tabsLeft: [buyerHome, buyerRequests],
  tabsRight: [buyerChats, buyerProfile],
  overflow: [savedLocations, savedSearches, buyerSettings],
  centerAction: { to: BUYER_NEW_REQUEST_PATH, label: 'request.new' },
}

// ─── Merchant ───────────────────────────────────────────────────────────────
export const MERCHANT_FEED_PATH = '/feed'

const feed: ShellNavItem = { key: 'feed', icon: 'i-heroicons-inbox-stack', label: 'merchant_nav.feed', to: MERCHANT_FEED_PATH }
const responses: ShellNavItem = { key: 'responses', icon: 'i-heroicons-check-circle', label: 'merchant_nav.responses' }
const merchantChats: ShellNavItem = { key: 'chats', icon: 'i-heroicons-chat-bubble-left-right', label: 'merchant_nav.chats' }
const merchantProfile: ShellNavItem = { key: 'profile', icon: 'i-heroicons-user', label: 'merchant_nav.profile' }
const shopSettings: ShellNavItem = { key: 'shop_settings', icon: 'i-heroicons-building-storefront', label: 'merchant_nav.shop_settings', to: '/merchant/subscriptions' }
const notifications: ShellNavItem = { key: 'notifications', icon: 'i-heroicons-bell', label: 'merchant_nav.notifications' }

export const merchantShell: ShellConfig = {
  roleLabel: 'merchant_nav.role_label',
  homePath: MERCHANT_FEED_PATH,
  sidebar: [feed, responses, merchantChats, merchantProfile, shopSettings, notifications],
  tabsLeft: [feed, responses],
  tabsRight: [merchantChats, merchantProfile],
  overflow: [shopSettings, notifications],
}

// --- Admin ---
export const ADMIN_CATALOGUE_PATH = '/admin/catalogue'

const adminDashboard: ShellNavItem = { key: 'dashboard', icon: 'i-heroicons-squares-2x2', label: 'admin_nav.dashboard' }
const adminUsers: ShellNavItem = { key: 'users', icon: 'i-heroicons-users', label: 'admin_nav.users' }
const adminMerchants: ShellNavItem = { key: 'merchants', icon: 'i-heroicons-building-storefront', label: 'admin_nav.merchants' }
const adminRequests: ShellNavItem = { key: 'requests', icon: 'i-heroicons-clipboard-document-list', label: 'admin_nav.requests' }
const adminCatalogue: ShellNavItem = { key: 'catalogue', icon: 'i-heroicons-folder', label: 'admin_nav.catalogue', to: ADMIN_CATALOGUE_PATH }
const adminReports: ShellNavItem = { key: 'reports', icon: 'i-heroicons-chart-bar', label: 'admin_nav.reports' }
const adminModeration: ShellNavItem = { key: 'moderation', icon: 'i-heroicons-shield-check', label: 'admin_nav.moderation' }
const adminSettings: ShellNavItem = { key: 'settings', icon: 'i-heroicons-cog-6-tooth', label: 'admin_nav.settings' }
const adminLogs: ShellNavItem = { key: 'system_logs', icon: 'i-heroicons-document-text', label: 'admin_nav.system_logs' }

export const adminShell: ShellConfig = {
  roleLabel: 'admin_nav.role_label',
  homePath: ADMIN_CATALOGUE_PATH,
  sidebar: [adminDashboard, adminUsers, adminMerchants, adminRequests, adminCatalogue, adminReports, adminModeration, adminSettings, adminLogs],
  tabsLeft: [adminCatalogue, adminUsers],
  tabsRight: [adminMerchants, adminRequests],
  overflow: [adminDashboard, adminReports, adminModeration, adminSettings, adminLogs],
}
