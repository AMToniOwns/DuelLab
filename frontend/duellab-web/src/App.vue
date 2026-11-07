<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useDeckEditorStore } from '@/stores/deckEditor'
import ToastStack from '@/components/ToastStack.vue'

const route = useRoute()
const editor = useDeckEditorStore()

const links = [
  { to: '/', label: 'Dashboard', exact: true },
  { to: '/decks', label: 'My decks' },
  { to: '/cards', label: 'Card database' },
  { to: '/simulate', label: 'Simulator' },
  { to: '/compare', label: 'Compare' },
]

function isActive(to: string, exact = false): boolean {
  if (exact) return route.path === to
  if (to === '/decks') return route.path.startsWith('/decks') || route.path.startsWith('/versions')
  if (to === '/simulate') return route.path.startsWith('/simulat')
  return route.path.startsWith(to)
}

// Theme: follow the OS by default; the toggle stores an explicit choice.
type Theme = 'system' | 'light' | 'dark'
const theme = ref<Theme>('system')
function readTheme(): Theme {
  try {
    const stored = localStorage.getItem('duellab-theme')
    return stored === 'light' || stored === 'dark' ? stored : 'system'
  } catch {
    return 'system'
  }
}
function cycleTheme(): void {
  theme.value = theme.value === 'system' ? 'dark' : theme.value === 'dark' ? 'light' : 'system'
  const root = document.documentElement
  try {
    if (theme.value === 'system') {
      delete root.dataset.theme
      localStorage.removeItem('duellab-theme')
    } else {
      root.dataset.theme = theme.value
      localStorage.setItem('duellab-theme', theme.value)
    }
  } catch {
    // Storage unavailable: the choice applies to this page only.
  }
}
const themeLabel = computed(
  () => ({ system: 'Theme: system', dark: 'Theme: dark', light: 'Theme: light' })[theme.value],
)

// Unsaved drafts: remind the user where they are, and warn before closing the tab.
const unsaved = computed(() => editor.dirtyVersionIds.filter((id) => route.params.versionId !== id))
function beforeUnload(event: BeforeUnloadEvent): void {
  if (editor.dirtyVersionIds.length > 0) event.preventDefault()
}
onMounted(() => {
  theme.value = readTheme()
  window.addEventListener('beforeunload', beforeUnload)
})
onBeforeUnmount(() => window.removeEventListener('beforeunload', beforeUnload))
</script>

<template>
  <div class="min-h-screen">
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:m-2 focus:rounded focus:bg-surface focus:p-2"
    >
      Skip to content
    </a>
    <header class="sticky top-0 z-30 border-b border-line bg-surface/90 backdrop-blur">
      <div class="mx-auto flex max-w-7xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-2.5">
        <RouterLink to="/" class="flex items-center gap-2 font-semibold tracking-tight">
          <img src="/favicon.svg" alt="" class="h-7 w-7" />
          <span>DuelLab</span>
        </RouterLink>
        <nav aria-label="Main" class="-mx-1 flex flex-1 gap-1 overflow-x-auto">
          <RouterLink
            v-for="link in links"
            :key="link.to"
            :to="link.to"
            class="whitespace-nowrap rounded-md px-2.5 py-1.5 text-sm text-ink-2 hover:bg-surface-2 hover:text-ink"
            :class="{ 'bg-accent-soft font-medium text-ink!': isActive(link.to, link.exact) }"
            :aria-current="isActive(link.to, link.exact) ? 'page' : undefined"
          >
            {{ link.label }}
          </RouterLink>
        </nav>
        <button
          type="button"
          class="btn btn-sm"
          :title="themeLabel"
          :aria-label="themeLabel"
          @click="cycleTheme"
        >
          <span aria-hidden="true">{{
            theme === 'dark' ? '☾' : theme === 'light' ? '☀' : '◑'
          }}</span>
          <span class="hidden sm:inline">{{
            theme === 'system' ? 'Auto' : theme === 'dark' ? 'Dark' : 'Light'
          }}</span>
        </button>
      </div>
      <div v-if="unsaved.length > 0" class="border-t border-line bg-warning-soft">
        <div class="mx-auto flex max-w-7xl flex-wrap items-center gap-2 px-4 py-1.5 text-sm">
          <span>You have unsaved deck changes.</span>
          <RouterLink
            v-for="id in unsaved"
            :key="id"
            :to="{ name: 'editor', params: { versionId: id } }"
            class="font-medium text-accent underline underline-offset-2"
          >
            {{ editor.versions[id]?.deckName ?? 'Deck' }} ·
            {{ editor.versions[id]?.versionName ?? 'version' }}
          </RouterLink>
        </div>
      </div>
    </header>

    <main id="main" class="mx-auto max-w-7xl px-4 py-6">
      <RouterView />
    </main>

    <footer class="mx-auto max-w-7xl px-4 pb-8 text-xs text-ink-muted">
      DuelLab is a statistics tool: role-based conditions measure how often a hand contains the
      cards you labelled, not whether a hand can actually complete a combo. Card data from
      YGOPRODeck. Yu-Gi-Oh! is a trademark of Konami.
    </footer>

    <ToastStack />
  </div>
</template>
