# Kontakt i podrška (SPEC-20)

> Implemented 2026-08-28. Spec: [`specs/SPEC-20-contact-support.md`](../specs/SPEC-20-contact-support.md).

## What it is

A modal with four direct channels to a human — WhatsApp, Viber, phone and e-mail — reachable from
**every** screen, signed in or not. Before it, the only channel was the SPEC-13 feedback form, which
requires a session; someone who could not sign in had nothing at all.

## Frontend only

No entity, no endpoint, no migration, no env var. The details are a compile-time constant in
`core/contact/contactInfo.ts`.

| File | Role |
|---|---|
| `core/contact/contactInfo.ts` | Number, e-mail, display forms, link builders, message prefill |
| `shared/components/ContactModal.tsx` | The dialog (built on the shared `Modal`) |
| `shared/components/ContactLink.tsx` | Self-contained trigger + own state, for the signed-out screens |

## Entry points

| Where | Owner of the modal state |
|---|---|
| Footer of every page | `Layout` |
| Profile dropdown (desktop) | `Layout` |
| Mobile hamburger panel | `Layout` |
| Login, Register | `ContactLink` |
| Forgot / Reset / Verify | `ContactLink`, via `AuthCard` |

`Layout` renders **one** `ContactModal` and opens it from all three of its triggers, the same way it
already handles `FeedbackFormModal`. The auth screens have no `Layout` above them, so `ContactLink`
carries its own state.

## Four things that are deliberate

**It is a modal, not a route.** `/login` and a `/kontakt` route would be two routes, so opening
contact from the sign-in screen would unmount the form and discard the typed email and password —
and the user who cannot sign in is exactly the one reaching for it. The accepted cost is that the
contact page has no shareable URL.

**The data is a constant, not a fetch.** The app is used offline in the field (SPEC-07). A number
fetched from the server would be missing precisely when the phone is the only channel left.

**Every row has a copy button, and copying has a fallback.** A `viber://` link on a desktop without
Viber installed does nothing at all — no error, no hint. If the Clipboard API is refused (plain
http, some in-app browsers) the handler selects the row's own text and says so, rather than failing
silently the way the link it exists to compensate for does.

**One address, everywhere.** `UPGRADE_EMAIL` in `core/services/planService.ts` is an alias of
`CONTACT_EMAIL`, not a second literal — before that, the plans page pointed upgrade requests at a
personal Gmail while this modal showed the official address.

**The contact address is `info@melarium.app`, never `noreply@melarium.app`.** The latter is the
Resend sending identity in `EmailService` / `SMTP_FROM_EMAIL`; showing it here would invite replies
to an address named "do not reply".

## Prefilled messages

The WhatsApp text and the `mailto` body carry the sender's name, organisation, role and current
route — but only from what `AuthContext` already holds. No extra query: a screen whose purpose is to
work when the network or the sign-in is broken must not depend on either.

Signed out, the subject becomes "Melarium — pomoć pri prijavi" and no personal details are added.

## Relationship to SPEC-13 feedback

They are not duplicates and do not merge:

| | Contact (SPEC-20) | Prijavi problem (SPEC-13) |
|---|---|---|
| When | Urgent, conversational | A report with evidence |
| Who | Anyone, including signed out | Signed-in users only |
| Carries | A message | Screenshot + DB row + triage |

## Not included

Terms of use and privacy policy (Melarium is not a registered company yet — separate topic), a
contact entry at the bottom of the help panel, on the 404 and crash screens, or in the command
palette. Those were considered and deferred, not overlooked.
