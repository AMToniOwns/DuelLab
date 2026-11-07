/** Formats a probability (0–1) as a percentage, e.g. 0.90514 → "90.5%". */
export function percent(value: number | null | undefined, digits = 1): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—'
  return `${(value * 100).toFixed(digits)}%`
}

/** Formats a percentage-point difference with its sign, e.g. 4.49 → "+4.5 pp". */
export function pointDelta(value: number | null | undefined, digits = 1): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—'
  const rounded = Number(value.toFixed(digits))
  if (rounded === 0) return `0.${'0'.repeat(digits)} pp`
  return `${rounded > 0 ? '+' : '−'}${Math.abs(rounded).toFixed(digits)} pp`
}

/** Formats a whole number with thousands separators. */
export function count(value: number): string {
  return value.toLocaleString('en-US')
}

/** Formats an ISO date-time as a short local date and time. */
export function dateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/** "3 minutes ago"-style relative time. */
export function relativeTime(iso: string, now: Date = new Date()): string {
  const seconds = Math.round((new Date(iso).getTime() - now.getTime()) / 1000)
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 31_536_000],
    ['month', 2_592_000],
    ['day', 86_400],
    ['hour', 3_600],
    ['minute', 60],
  ]
  const format = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' })
  for (const [unit, size] of units) {
    if (Math.abs(seconds) >= size) return format.format(Math.round(seconds / size), unit)
  }
  return format.format(seconds, 'second')
}
