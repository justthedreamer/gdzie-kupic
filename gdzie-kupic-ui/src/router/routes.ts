import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: () => import('layouts/MainLayout.vue'),
    children: [
      { path: '', component: () => import('pages/IndexPage.vue') },
      { path: 'post/:id', component: () => import('pages/PostDetailPage.vue') },
      {
        path: 'post/:id/response/:responseId',
        component: () => import('pages/MerchantResponsePage.vue'),
      },
      { path: 'merchant', component: () => import('pages/MerchantHomePage.vue') },
    ],
  },

  // Always leave this as last one
  {
    path: '/:catchAll(.*)*',
    component: () => import('pages/ErrorNotFound.vue'),
  },
];

export default routes;
