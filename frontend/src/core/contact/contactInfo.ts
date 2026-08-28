/**
 * Support contact details (SPEC-20).
 *
 * These are a compile-time constant rather than a setting or an API call, deliberately (D2): they
 * do not need to change without a deploy, and the app is used offline in the field (SPEC-07) — a
 * number fetched from the server would be missing exactly when the phone is the only channel left.
 */

/** E.164. One number answers calls, Viber and WhatsApp. */
export const CONTACT_PHONE_E164 = '+387603209030'

/** How the number is written for a human. Never parse this — build links from the E.164 form. */
export const CONTACT_PHONE_DISPLAY = '+387 60 32 09 030'

/**
 * Deliberately *not* `noreply@melarium.app` (D3): that address is the Resend sending identity used
 * by `EmailService`, and its name tells the reader not to reply — the opposite of this screen.
 */
export const CONTACT_EMAIL = 'info@melarium.app'

/** Stated once so the modal and any later copy cannot drift apart. There are no working hours. */
export const CONTACT_RESPONSE_PROMISE = 'Odgovaramo u roku od 24 sata.'

// ── Link builders ─────────────────────────────────────────────────────────────

/** wa.me takes the number without the leading '+'. */
export function whatsappUrl(text?: string): string {
  const number = CONTACT_PHONE_E164.slice(1)
  return text ? `https://wa.me/${number}?text=${encodeURIComponent(text)}` : `https://wa.me/${number}`
}

/**
 * Viber takes no message parameter — it can only open the chat. On a desktop without Viber
 * installed this silently does nothing, which is why every row carries a copy button (D4).
 */
export function viberUrl(): string {
  return `viber://chat?number=${encodeURIComponent(CONTACT_PHONE_E164)}`
}

export function telUrl(): string {
  return `tel:${CONTACT_PHONE_E164}`
}

export function mailtoUrl(subject: string, body: string): string {
  return `mailto:${CONTACT_EMAIL}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`
}

// ── Prefilled messages ────────────────────────────────────────────────────────

/**
 * Everything the prefill knows about the sender. Only fields `AuthContext` already holds (D5) —
 * the modal must not fire a network request on a screen whose whole purpose is to work when the
 * network or the sign-in is broken.
 */
export interface ContactContext {
  firstName: string
  lastName: string
  email: string
  /** Raw role name (`OrganizationAdmin`, …). Diagnostic detail for us, not user-facing prose. */
  role: string
  organizationName?: string | null
  /** Route the user was on when they opened the modal. */
  pathname?: string
}

/** Signed out, we know nothing about the sender — and the reason is almost always the same one. */
const SIGNED_OUT_SUBJECT = 'Melarium — pomoć pri prijavi'
const SIGNED_OUT_BODY = 'Zdravo,\n\nimam problem s prijavom u Melarium.\n\n'

export function buildEmailMessage(ctx: ContactContext | null): { subject: string; body: string } {
  if (!ctx) return { subject: SIGNED_OUT_SUBJECT, body: SIGNED_OUT_BODY }

  const details = [
    `Ime: ${ctx.firstName} ${ctx.lastName}`,
    `Email: ${ctx.email}`,
    ctx.organizationName ? `Organizacija: ${ctx.organizationName}` : null,
    `Uloga: ${ctx.role}`,
    ctx.pathname ? `Stranica: ${ctx.pathname}` : null,
  ].filter(Boolean).join('\n')

  // The dashed line marks where the user's own text ends — without it the auto-added block reads
  // as if they typed it.
  return {
    subject: 'Melarium — upit',
    body: `Zdravo,\n\n\n—\n${details}\n`,
  }
}

/** WhatsApp opens with this in the input box, so it stays to one line — nobody sends a form. */
export function buildWhatsappText(ctx: ContactContext | null): string {
  if (!ctx) return 'Zdravo, imam problem s prijavom u Melarium.'

  const who = ctx.organizationName
    ? `${ctx.firstName} ${ctx.lastName}, ${ctx.organizationName}`
    : `${ctx.firstName} ${ctx.lastName}`
  return `Zdravo, trebam pomoć oko Melariuma. (${who})`
}
