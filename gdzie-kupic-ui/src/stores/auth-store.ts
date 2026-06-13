import { defineStore, acceptHMRUpdate } from 'pinia';

export type UserRole = 'guest' | 'user' | 'merchant';

export const useAuthStore = defineStore('auth', {
  state: () => ({
    role: 'guest' as UserRole,
    token: null as string | null,
  }),

  getters: {
    isAuthenticated: (state) => state.role !== 'guest',
    isMerchant: (state) => state.role === 'merchant',
  },

  actions: {
    setRole(role: UserRole) {
      this.role = role;
    },
    logout() {
      this.role = 'guest';
      this.token = null;
    },
  },
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useAuthStore, import.meta.hot));
}
