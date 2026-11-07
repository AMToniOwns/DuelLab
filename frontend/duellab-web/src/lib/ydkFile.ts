/** Largest .ydk file accepted (the server has the same limit; a full deck is under 2 KB). */
export const MAX_YDK_BYTES = 64 * 1024

/** Reads a chosen .ydk file as text, rejecting files that cannot be deck files. */
export async function readYdkFile(file: File): Promise<string> {
  if (file.size > MAX_YDK_BYTES)
    throw new Error(`${file.name} is too large to be a .ydk deck file.`)
  return await file.text()
}

/** A deck or version name from a file name: "Sky_Striker.ydk" → "Sky Striker". */
export function nameFromFileName(fileName: string): string {
  const base = fileName
    .replace(/\.ydk$/i, '')
    .replace(/[_]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
  return (base || 'Imported deck').slice(0, 100)
}
