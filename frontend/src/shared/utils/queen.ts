import { QueenMarkColor } from '../../core/models'

/** Season ordinal for a queen born in `year` — the birth year counts as the 1st season. */
export const queenSeason = (year: number): number =>
  Math.max(1, new Date().getFullYear() - year + 1)

/**
 * International marking color derived from the birth year
 * (1/6 white, 2/7 yellow, 3/8 red, 4/9 green, 5/0 blue).
 * Mirrors `QueenMarkColorHelper` on the backend.
 */
export const queenColorForYear = (year: number): QueenMarkColor => {
  switch (year % 10) {
    case 1:
    case 6:
      return QueenMarkColor.White
    case 2:
    case 7:
      return QueenMarkColor.Yellow
    case 3:
    case 8:
      return QueenMarkColor.Red
    case 4:
    case 9:
      return QueenMarkColor.Green
    default:
      return QueenMarkColor.Blue
  }
}

/** Tailwind classes for the physical mark-color dot. */
export const queenColorDotClass: Record<QueenMarkColor, string> = {
  [QueenMarkColor.White]:  'bg-white border-2 border-slate-300 dark:border-slate-400',
  [QueenMarkColor.Yellow]: 'bg-yellow-400',
  [QueenMarkColor.Red]:    'bg-red-500',
  [QueenMarkColor.Green]:  'bg-green-500',
  [QueenMarkColor.Blue]:   'bg-blue-500',
}
