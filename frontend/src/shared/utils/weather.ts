// ── WMO weather code → emoji + label ─────────────────────────────────────────
// Shared by the apiary page and the dashboard (SPEC-29), so one code never gets two names.

export function wmoToIcon(code?: number | null): string {
  if (code == null) return '🌡️'
  if (code === 0)                   return '☀️'
  if (code === 1)                   return '🌤️'
  if (code === 2)                   return '⛅'
  if (code === 3)                   return '☁️'
  if (code === 45 || code === 48)   return '🌫️'
  if (code >= 51 && code <= 55)     return '🌦️'
  if (code >= 61 && code <= 65)     return '🌧️'
  if (code >= 71 && code <= 77)     return '🌨️'
  if (code >= 80 && code <= 82)     return '🌧️'
  if (code >= 85 && code <= 86)     return '🌨️'
  if (code >= 95 && code <= 99)     return '⛈️'
  return '🌡️'
}

export function wmoToLabel(code?: number | null): string {
  if (code == null) return 'Nepoznato'
  if (code === 0)                   return 'Vedro nebo'
  if (code === 1)                   return 'Uglavnom vedro'
  if (code === 2)                   return 'Djelimično oblačno'
  if (code === 3)                   return 'Oblačno'
  if (code === 45 || code === 48)   return 'Magla'
  if (code >= 51 && code <= 55)     return 'Rosulja'
  if (code >= 61 && code <= 65)     return 'Kiša'
  if (code >= 71 && code <= 77)     return 'Snijeg'
  if (code >= 80 && code <= 82)     return 'Pljuskovi kiše'
  if (code >= 85 && code <= 86)     return 'Pljuskovi snijega'
  if (code >= 95 && code <= 99)     return 'Grmljavina'
  return 'Nepoznato'
}
