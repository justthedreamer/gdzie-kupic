<template>
  <q-layout view="lHh Lpr lFf">
    <q-header elevated>
      <q-toolbar>
        <q-btn flat dense round icon="menu" aria-label="Menu" @click="toggleLeftDrawer" />
        <q-toolbar-title>Gdzie Kupić</q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-drawer v-model="leftDrawerOpen" show-if-above bordered>
      <q-list padding>
        <q-item clickable v-ripple to="/" exact>
          <q-item-section avatar>
            <q-icon name="home" />
          </q-item-section>
          <q-item-section>Home</q-item-section>
        </q-item>

        <q-item v-if="!isMerchant" clickable v-ripple to="/posts">
          <q-item-section avatar>
            <q-icon name="history" />
          </q-item-section>
          <q-item-section>Post History</q-item-section>
        </q-item>

        <q-item v-if="isMerchant" clickable v-ripple to="/responses">
          <q-item-section avatar>
            <q-icon name="history" />
          </q-item-section>
          <q-item-section>Response History</q-item-section>
        </q-item>

        <template v-if="!isAuthenticated">
          <q-item clickable v-ripple to="/register">
            <q-item-section avatar>
              <q-icon name="person_add" />
            </q-item-section>
            <q-item-section>Register</q-item-section>
          </q-item>

          <q-item clickable v-ripple to="/login">
            <q-item-section avatar>
              <q-icon name="login" />
            </q-item-section>
            <q-item-section>Login</q-item-section>
          </q-item>
        </template>

        <q-item v-else clickable v-ripple @click="authStore.logout()">
          <q-item-section avatar>
            <q-icon name="logout" />
          </q-item-section>
          <q-item-section>Logout</q-item-section>
        </q-item>
      </q-list>
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { useAuthStore } from 'stores/auth-store';

const authStore = useAuthStore();
const isAuthenticated = computed(() => authStore.isAuthenticated);
const isMerchant = computed(() => authStore.isMerchant);

const leftDrawerOpen = ref(false);

function toggleLeftDrawer() {
  leftDrawerOpen.value = !leftDrawerOpen.value;
}
</script>
