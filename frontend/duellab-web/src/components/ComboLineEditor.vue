<script setup lang="ts">
import type { ComboPiece } from '@/api/types'
import type { DraftComboLine } from '@/lib/deckDraft'
import TargetPicker, { type PickableCard } from './TargetPicker.vue'

/**
 * Edits a version's combo lines: the pieces needed in the opening hand. Each piece is one card (or
 * up to three) from a set of alternatives, e.g. "Snake-Eye Ash or a card that searches it".
 */
const lines = defineModel<DraftComboLine[]>({ required: true })
defineProps<{ roles: string[]; cards: PickableCard[] }>()
const emit = defineEmits<{ changed: [] }>()

const MAX_LINES = 12
const MAX_PIECES = 10

function newPiece(): ComboPiece {
  return { cardIds: [], roles: [], count: 1 }
}

function removeLine(index: number): void {
  lines.value.splice(index, 1)
  emit('changed')
}

function addPiece(line: DraftComboLine): void {
  line.pieces.push(newPiece())
  emit('changed')
}

function removePiece(line: DraftComboLine, index: number): void {
  line.pieces.splice(index, 1)
  emit('changed')
}

function addLine(): void {
  lines.value.push({ name: `Line ${lines.value.length + 1}`, notes: null, pieces: [newPiece()] })
  emit('changed')
}
</script>

<template>
  <section class="space-y-3" aria-labelledby="combo-heading">
    <div>
      <h2 id="combo-heading" class="text-sm font-semibold">Combo lines</h2>
      <p class="text-xs text-ink-2">
        List the pieces a line needs in your opening hand. A piece can be any of several cards, for
        example the combo card
        <em>or</em> a card that searches it. DuelLab checks the hand holds every piece, using a
        different card for each; it does not play the combo out.
      </p>
    </div>

    <div
      v-for="(line, li) in lines"
      :key="li"
      class="rounded-lg border border-line p-3"
      :data-testid="`combo-line-${li}`"
    >
      <div class="flex items-center gap-2">
        <label class="sr-only" :for="`combo-name-${li}`">Combo line name</label>
        <input
          :id="`combo-name-${li}`"
          v-model="line.name"
          class="input font-medium"
          maxlength="100"
          @input="emit('changed')"
        />
        <button
          type="button"
          class="btn btn-sm btn-danger"
          :aria-label="`Remove combo line ${line.name}`"
          @click="removeLine(li)"
        >
          Remove
        </button>
      </div>
      <ol class="mt-2 space-y-1.5">
        <li
          v-for="(piece, pi) in line.pieces"
          :key="pi"
          class="flex flex-wrap items-center gap-2 text-sm"
        >
          <span class="num w-14 text-xs text-ink-muted">Piece {{ pi + 1 }}</span>
          <label class="sr-only" :for="`piece-count-${li}-${pi}`">Cards needed</label>
          <select
            :id="`piece-count-${li}-${pi}`"
            v-model.number="piece.count"
            class="input w-auto! py-0.5! text-xs!"
            @change="emit('changed')"
          >
            <option :value="1">1 of</option>
            <option :value="2">2 of</option>
            <option :value="3">3 of</option>
          </select>
          <TargetPicker
            v-model:roles="piece.roles"
            v-model:card-ids="piece.cardIds"
            :available-roles="roles"
            :cards="cards"
            :label="`piece-${li}-${pi}`"
            @update:roles="emit('changed')"
            @update:card-ids="emit('changed')"
          />
          <button
            v-if="line.pieces.length > 1"
            type="button"
            class="text-ink-muted hover:text-critical"
            :aria-label="`Remove piece ${pi + 1}`"
            @click="removePiece(line, pi)"
          >
            ✕
          </button>
          <span v-if="piece.cardIds.length + piece.roles.length === 0" class="text-xs text-critical"
            >Choose at least one card or role.</span
          >
        </li>
      </ol>
      <div class="mt-2 flex flex-wrap items-center gap-2">
        <button
          type="button"
          class="btn btn-sm"
          :disabled="line.pieces.length >= MAX_PIECES"
          @click="addPiece(line)"
        >
          + Piece
        </button>
        <label class="sr-only" :for="`combo-notes-${li}`">Notes</label>
        <input
          :id="`combo-notes-${li}`"
          v-model="line.notes"
          class="input flex-1 text-xs!"
          placeholder="Notes (optional), e.g. what it ends on"
          maxlength="2000"
          @input="emit('changed')"
        />
      </div>
    </div>

    <button
      type="button"
      class="btn btn-sm"
      :disabled="lines.length >= MAX_LINES"
      data-testid="add-combo-line"
      @click="addLine"
    >
      + Combo line
    </button>
  </section>
</template>
