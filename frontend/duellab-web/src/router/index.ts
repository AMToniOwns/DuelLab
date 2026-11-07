import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from '@/views/DashboardView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardView, meta: { title: 'Dashboard' } },
    {
      path: '/decks',
      name: 'decks',
      component: () => import('@/views/DecksView.vue'),
      meta: { title: 'My decks' },
    },
    {
      path: '/decks/:deckId',
      name: 'deck',
      component: () => import('@/views/DeckView.vue'),
      props: true,
      meta: { title: 'Deck' },
    },
    {
      path: '/versions/:versionId/edit',
      name: 'editor',
      component: () => import('@/views/DeckEditorView.vue'),
      props: true,
      meta: { title: 'Deck editor' },
    },
    {
      path: '/cards',
      name: 'cards',
      component: () => import('@/views/CardsView.vue'),
      meta: { title: 'Card database' },
    },
    {
      path: '/simulate',
      name: 'simulate',
      component: () => import('@/views/SimulatorView.vue'),
      meta: { title: 'Simulator' },
    },
    {
      path: '/simulations/:runId',
      name: 'run',
      component: () => import('@/views/SimulationResultView.vue'),
      props: true,
      meta: { title: 'Simulation results' },
    },
    {
      path: '/compare',
      name: 'compare',
      component: () => import('@/views/CompareView.vue'),
      meta: { title: 'Compare versions' },
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue'),
      meta: { title: 'Not found' },
    },
  ],
  scrollBehavior: (_to, _from, saved) => saved ?? { top: 0 },
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${String(to.meta.title)} · DuelLab` : 'DuelLab'
})

export default router
