import { differenceInCalendarDays, parseISO, startOfToday } from 'date-fns'
import { SeasonPhase } from '../../core/models'

const WEEKDAYS = ['ned', 'pon', 'uto', 'sri', 'čet', 'pet', 'sub']
const WEEKDAYS_LONG = ['nedjelja', 'ponedjeljak', 'utorak', 'srijeda', 'četvrtak', 'petak', 'subota']

const pad = (n: number) => String(n).padStart(2, '0')

/** "01.10." — a date-only value from the API (yyyy-MM-dd), read as a local calendar day. */
export function dayMonth(iso: string): string {
  const d = parseISO(iso)
  return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.`
}

export function daysFromToday(iso: string): number {
  return differenceInCalendarDays(parseISO(iso), startOfToday())
}

/** "Danas", "Sutra", or "pon 28.09." */
export function relativeDay(iso: string): string {
  const diff = daysFromToday(iso)
  if (diff === 0) return 'Danas'
  if (diff === 1) return 'Sutra'
  if (diff === -1) return 'Jučer'
  return `${WEEKDAYS[parseISO(iso).getDay()]} ${dayMonth(iso)}`
}

/** Short weekday for a forecast column: "danas", "sutra", "pon". */
export function forecastDay(iso: string): string {
  const diff = daysFromToday(iso)
  if (diff === 0) return 'danas'
  if (diff === 1) return 'sutra'
  return WEEKDAYS[parseISO(iso).getDay()]
}

export function todayLong(now = new Date()): string {
  return `${WEEKDAYS_LONG[now.getDay()]}, ${pad(now.getDate())}.${pad(now.getMonth() + 1)}.`
}

export function greeting(now = new Date()): string {
  const h = now.getHours()
  if (h >= 5 && h < 11) return 'Dobro jutro'
  if (h >= 11 && h < 18) return 'Dobar dan'
  return 'Dobro veče'
}

/** Bosnian count + noun: 1 košnica, 2–4 košnice, 5+ košnica, 11–14 many. */
export function count(n: number, one: string, few: string, many: string): string {
  const lastTwo = n % 100
  const last = n % 10
  const word = lastTwo >= 11 && lastTwo <= 14 ? many : last === 1 ? one : last >= 2 && last <= 4 ? few : many
  return `${n} ${word}`
}

export const SEASON_ICONS: Record<SeasonPhase, string> = {
  [SeasonPhase.Winter]:        '❄️',
  [SeasonPhase.SpringBuildUp]: '🌱',
  [SeasonPhase.MainSeason]:    '🌼',
  [SeasonPhase.LateSummer]:    '🌾',
  [SeasonPhase.Wintering]:     '🍂',
}

const MONTHS_LOCATIVE = [
  'januaru', 'februaru', 'martu', 'aprilu', 'maju', 'junu',
  'julu', 'augustu', 'septembru', 'oktobru', 'novembru', 'decembru',
]

/** "u septembru" — the locative a month takes after "u". */
export function inMonth(now = new Date()): string {
  return `u ${MONTHS_LOCATIVE[now.getMonth()]}`
}

export function formatKg(kg: number): string {
  return `${kg.toLocaleString('bs-BA', { maximumFractionDigits: 1 })} kg`
}
