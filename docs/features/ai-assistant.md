# Feature: AI Assistant (AI Asistent)

## Overview

A Bosnian-language AI assistant that does two things in one conversation. The beekeeper says or types
what they did — *"Pregledana košnica 2 na pčelinjaku Zlatna dolina, 5 ramova legla, med zadovoljavajući,
pregled za 10 dana"* — and the assistant finds the apiary and hive itself, proposes the records, and
creates them — or updates, completes or deletes an existing one it finds by title or date — **only
after an explicit confirmation**. Or the beekeeper asks a question — *"Kad se vrca lipov med?"* — and
gets a full answer immediately, grounded in a specific hive's real data when one is in scope. Implemented
per [SPEC-17](../specs/SPEC-17-ai-assistant.md) (Phases A/B/C: the command pipeline) and
[SPEC-18](../specs/SPEC-18-ai-merge.md) (Q&A, merged in from the retired **AI Advisor**/SPEC-01). Reuses
the Groq stack — no new provider, package or secret.

There used to be two AI surfaces — the assistant for actions, the advisor for questions — kept apart
because a router guessing "question or command" seemed like a new failure mode and a chat bubble seemed
like a poor host for an editable form (SPEC-17 §D2). Neither objection survived contact with how the
pipeline actually turned out: the model's JSON envelope already allows an empty `actions` array, so a
question needs no separate router step, and proposal cards were never rendered inside the reply bubble
to begin with — Q&A just uses the `reply` field the envelope always had. See SPEC-18 §0 and the ADR-033
addendum for the full reasoning.

## The rule the whole feature rests on (ADR-033)

`AiActionExecutor` builds the **same DTOs the forms post** and calls the **existing** application
services — `InspectionService.CreateAsync`, `TodoService.CreateAsync` — never repositories. That single
choice inherits `IAccessGuard`, the plan limits, the automatic weather temperature, and the todo
notification cascade, with no second implementation to keep in sync. Two consequences that are easy to
miss and are therefore explicit in the code:

- **The executor runs the validators itself.** FluentValidation lives in the controllers here, so
  without this AI-authored data would pass *fewer* checks than typed data.
- **The confirm request is untrusted input.** The card is editable, so the client can send any hive id;
  the confirm path re-resolves the target against the caller's accessible set and re-validates.

## Pipeline (`Melarium.Application/Features/Assistant/`)

- `AssistantPromptBuilder` — **pure**. System prompt: the shared beekeeping glossary, the action
  catalogue, and the org's apiary names in a fenced *data* block. "Today" comes from `AppTimeZone`,
  never `DateTime.UtcNow`. **SPEC-18**: takes an optional `contextBlock` string (a hive's grounding
  data, when one is in scope — see Q&A below) and instructs the model to answer a non-command message
  fully in `reply` instead of the old deflection to the (now-retired) advisor; the advisor's safety
  guardrails (AFB/EFB mandatory reporting, no dosing beyond manufacturer instructions, decline
  non-beekeeping topics) moved into this prompt verbatim.
- `IAssistantAiClient` / `GroqAssistantAiClient` (`Features/Ai/`) — model from `GroqModels.Chat`
  (`Groq:ChatModel`), temp 0, `response_format: json_object`. **A replacement model must support
  JSON mode**; without it the envelope never parses and every command dies as "AI nije dostupan".
- `AiEnvelopeParser` — **pure and total**. Never throws on model output: an unusable envelope returns
  null, a single bad action is dropped so the rest of the turn survives. Strips a stray ```json fence,
  rejects out-of-range enums and non-`yyyy-MM-dd` dates.
- `AiTargetResolver` — **pure**. Names/numbers → real entities, searching *only* the sets from
  `IAccessGuard`. Apiary by normalized name (diacritics folded, **`đ` → `dj`** — the transliteration
  Whisper and phone keyboards produce) → `contains` → the sole apiary. Hives via `HiveNumberMatcher`.
  `"all"` expands over one apiary only. Ambiguity is carried out as candidates, never guessed.
  **Phase C** added `ResolveExistingTodo` (match by title, same normalization as an apiary name,
  narrowed by a named hive/apiary) and `ResolveExistingInspection` (match by date within one resolved
  hive; no date = the most recent inspection). Both search only `AiResolutionContext.Todos`/
  `Inspections` — populated, and only when needed, by `AiAssistantService`.
- `AssistantClarificationBuilder` (Phase B, extended in Phase C) — **pure**. Turns an unresolved
  action's candidates (or, absent those, the model's own `needs` against the full accessible set) into
  tappable buttons, capped at 8 so a large organization falls back to the question text plus the card's
  dropdown instead of a button wall. The ceiling check always wins. Two todos sharing a title become
  buttons labelled by hive/apiary (the bare title would match both again); two inspections on the same
  date become buttons by date.
- `AiActionExecutor` — validators → existing services → per-action outcome. **Phase C**: `UpdateTodo`/
  `CompleteTodo`/`DeleteTodo`/`UpdateInspection`/`DeleteInspection` fetch the current record, apply only
  the fields the AI actually mentioned (`null` = "don't touch this"), and call the same
  `TodoService`/`InspectionService` methods the edit forms use — no new repository code.
- `AiAssistantService` — ownership, quota, pre-flight, persistence, confirmation.

## Service rules

- **Ownership** from `ICurrentUser`; another user's session or turn is **404**, never 403.
- **Transactional AI:** an AI failure throws `BusinessRuleException` and persists nothing, so the
  beekeeper's words are never lost to a Groq outage.
- **Pre-flight:** targets are resolved and access-checked *at propose time*, so an action that is
  certain to fail (e.g. an apiary todo a Beekeeper cannot create) never becomes a confirmable card.
- **Partial success is reported, not rolled back.** Each service calls `SaveChangesAsync` itself, so a
  mid-batch failure cannot cleanly undo the earlier writes. The result names what landed and what did not.
- **Double-confirm guard:** confirmation claims the rows (`Pending → Confirmed`) and saves *before*
  executing, so a phone double-tap finds nothing pending and is refused rather than duplicating records.
- **Ceiling:** `Ai:MaxActionsPerCommand` (default 50). Over it nothing is offered; the reply states the
  real count and asks the user to narrow the command.
- Session cap **40 turns**, history window **8 turns**, message length 1–4000 (FluentValidation).
- **Clarification (Phase B):** candidates are computed once per turn and persisted (`CandidatesJson`),
  so re-opening a session reproduces the same buttons. `ToDetail` zeroes out every assistant turn's
  candidates except the latest, so an answered question can never be tapped twice. The system prompt
  carries an explicit continuation rule: a short follow-up ("Livada") is combined with the fields the
  beekeeper already dictated earlier in the same session, not treated as an isolated new command.
- **Existing-record target is fixed (Phase C):** an update/delete's `ExistingEntityId` is set once at
  propose time and is never re-picked from the confirm request — there is no dropdown for "a different
  todo" the way there is for a new record's apiary/hive. The executor still re-fetches the record and
  the underlying service still re-runs its own access check, so a record that changed or became
  inaccessible between propose and confirm fails the same way an edit form would (§5.3 unchanged).
- **`IsDestructive` (Phase C, SPEC-17 D5/§7.3):** computed from the action kind — `UpdateTodo`,
  `UpdateInspection`, `DeleteTodo`, `DeleteInspection`. **Not** `CompleteTodo`: checking a todo off is a
  one-tap, instantly-reversible toggle everywhere else in the app (the plain checkbox asks nothing), so
  holding the assistant to a stricter bar than the rest of the UI would be inventing risk that is not
  there. A batch's second confirmation triggers only when it contains a destructive action.

## Q&A (SPEC-18)

A turn with an empty `actions` array and a full `reply` **is** the answer to a question — no separate
code path, no classification step before the model call. What SPEC-18 adds is *grounding*:

- **Trigger:** a beehive in scope — `dto.BeehiveId` on the turn, or (new) the session's own stored
  `BeehiveId` once one has been set. Chosen over gating on "the envelope turned out to have zero
  actions" because that fact is only known *after* the model call that would need the context — gating
  on it would mean either a second, slower call, or a pre-call classifier duplicating what the single
  call already does reliably. This is the exact trigger the advisor used from SPEC-01 onward.
- **Data:** `HiveContextBuilder.Build(...)` (moved from the advisor's `AdvisorContextBuilder`, pure,
  unchanged) renders hive + apiary + last 5 inspections + active diets + open todos + queen + season
  yield + latest treatment + latest apiary move + best-effort weather into a compact Bosnian block,
  assembled by a new `AiAssistantService.BuildHiveContextBlockAsync` that mirrors
  `AdvisorService.BuildContextBlockAsync`'s repository calls exactly.
- **Access control this introduces:** a `beehiveId` was previously only a resolver tie-breaker,
  harmless if it belonged to someone else — an inaccessible id just failed to match anything. Actively
  *reading* that hive's data into a prompt is a different trust level, so the same discipline the
  advisor already had applies: `EnsureCanAccessBeehiveAsync` (throws) when a session is first bound to
  a hive; `CanAccessBeehiveAsync` (non-throwing) on every later turn, silently dropping context — never
  failing the turn — if access was withdrawn since.
- **Session-level hive binding:** `AiAssistantSession` gained a nullable `BeehiveId` (migration
  `AddAiAssistantSessionBeehive`, `SET NULL` on hive delete, same policy `AdvisorConversation` used) so
  reopening an old session from history keeps its 🐝 chip and its grounding, not just the turn that
  first established it.

## Domain

`AiAssistantSession` (owner, cascade, **`BeehiveId?`** — SPEC-18) → `AiAssistantTurn` (`Role`, `Content`, `Transcript?`,
`RawModelJson?`, `CandidatesJson?`) → `AiAssistantAction` (`Kind`, `PayloadJson`, `TargetSummary`,
`Status`, `ResultEntityType?`, `ResultEntityId?`, `ErrorMessage?`). Migrations `AddAiAssistant` (three
new tables) and `AddAssistantClarification` (`CandidatesJson` column, Phase B) — nothing existing
altered. `ResultEntityId` is deliberately **not** a foreign key: deleting the created inspection must not
delete the record that the assistant created it.

`AiActionKind` extends to `UpdateTodo = 3, CompleteTodo = 4, UpdateInspection = 5, DeleteTodo = 6,
DeleteInspection = 7` (Phase C) — no schema change, since the existing record's id, "before" values and
whether the action is destructive all live inside `PayloadJson`, the same free-form column Phase A
already used for an unresolved action's candidates.

## Plans

`PlanGuard.EnsureAiInteractionAsync` (SPEC-18; replaces the former separate `EnsureAiCommandAsync` and
the advisor's `EnsureAdvisorMessageAsync`) — Free throws, otherwise the org's user turns this UTC month
are compared against **one** combined `Plans:{Plan}:AiInteractionsPerMonth` (Standard = 30, absent =
unlimited). One counter, not two, because the gate runs *before* the model call — the only point at
which a turn could be pre-classified as "question" or "command" is after that call returns, so a
combined pre-flight number is what the ordering actually requires, not just a simplification. Charged
on **interpretation**, not confirmation: a rejected proposal, or a question that got a full answer,
both still consumed a Groq call. 402 reaches the existing upsell modal through `apiClient`.

## API (`/api/assistant`)

| Method | Path | Notes |
|---|---|---|
| GET | `/sessions` | own summaries, newest activity first — each carries `beehiveId`/`beehiveName` (SPEC-18) |
| GET | `/sessions/{id}` | thread with turns + actions (404 if not owner) |
| POST | `/sessions` | `{ text, transcript?, apiaryId?, beehiveId? }` → 201; `ai-chat` 10/min |
| POST | `/sessions/{id}/turns` | continues the thread; `ai-chat` |
| POST | `/turns/{id}/confirm` | `{ actions: [{ id, apiaryId?, beehiveId?, fields }] }` → per-action results. `apiaryId`/`beehiveId` are ignored for Phase C's existing-record kinds — see below |
| POST | `/turns/{id}/reject` | 204 |
| POST | `/transcribe` | multipart → `{ transcript }`; `voice-parse`, 15 MB |
| DELETE | `/sessions/{id}` | 204 (owner only) |

No new rate-limit policy — `ai-chat` and `voice-parse` already exist.

Each action in a turn's response also carries `isDestructive: bool` and, for Phase C kinds,
`previousFields` — the existing record's current values, so the frontend can show "prije → poslije"
without a second request.

## Frontend (`features/assistant/`)

- `AssistantSheet` — the sheet itself, mounted once in `Layout`, on every page. Passes the route's
  apiary/hive as context, which fills gaps only. Closes on navigation (its context would otherwise be
  stale) and when the assistant stops being available mid-session.
- **On a phone it is a real bottom sheet** (2026-08-17): grab handle, drag down past ~110 px to
  dismiss, spring back below that. Three things it had been getting wrong, all reported as "izgleda
  loše / sadržaj iza se skrolla":
  - It was the **only overlay in the app not using `useDialogBehavior`**, so it alone had no body
    scroll lock, no focus trap and no focus restore. It uses the shared hook now — do not hand-roll
    a bare Escape listener here again.
  - **`h-[85dvh]`, never `vh`.** On a phone `vh` measures the viewport with the URL bar *hidden*, so
    `85vh` reached past the visible area and pushed the input row — the entire point of the sheet —
    off the bottom of the screen.
  - `overscroll-contain` on the thread, and the auto-scroll sets the thread's own `scrollTop`
    instead of `scrollIntoView` on a bottom marker: `scrollIntoView` walks up and scrolls **every**
    scrollable ancestor, which is what dragged the page behind the overlay along with it.

  The drag transform is applied **only while a drag is in progress**, deliberately: a transform makes
  the panel a containing block, and `AssistantThread` renders a `fixed inset-0` confirmation modal
  inside it. It also must not call `onClose()` from inside a `setState` updater — that runs during
  render, so it updates `Layout` mid-render (React warns; StrictMode can fire it twice). The drag
  distance is mirrored in a ref for exactly that reason.
- **Opening it belongs to `Layout`, not to this component** (was `AssistantLauncher`, which owned its
  own button and `open` state until 2026-08-17). The button shares the bottom-right corner with the QR
  scanner, and two components each placing themselves `fixed` there is exactly how they ended up on top
  of each other on a phone. `shared/components/FabDock` now owns that corner: both are entries in one
  list, rendered as a single capsule with a half each, and `Layout` decides availability — hidden
  offline (transcription and interpretation are server-side) and on `/assistant` itself, where a
  shortcut to the page you are on is noise. On desktop the scanner half drops out (`sm:hidden` — the
  header already carries scanning) and the capsule collapses back to an ordinary round FAB.
- `AssistantThread` — the thread: input (textarea + mic), messages, proposal cards, confirm bar. A
  spoken command lands in the box for review and is **never** auto-sent — it can create records. An
  assistant reply renders through the shared `MarkdownMessage` (SPEC-18, moved out of the retired
  advisor's `ChatThread`) so a Q&A answer's headings/lists/bold render properly; user turns stay plain
  text. A persistent footer disclaims that advice is informational and points AFB/EFB-type suspicions to
  a vet — ported from the advisor, since the merged assistant now sometimes gives that class of answer.
- **Every failure must be reported to the user here.** Until 2026-08-17 `send`, `confirmSelected` and
  `rejectAll` each swallowed the rejection under a comment claiming apiClient displayed it. It does
  not — `apiClient` only *rejects with* the message; nothing in it renders anything except the
  `plan-limit` upsell event. So a rate limit, an expired plan, a Groq outage or a timeout all looked
  identical from a phone: tap Pošalji, spinner stops, **nothing happens** — reported as "the AI
  assistant doesn't work". The backend was already sending a perfectly good Bosnian message
  (`AiAssistantService.CallAiAsync` turns any upstream failure into
  *"AI servis trenutno nije dostupan…"*); the UI was throwing it away.
  Use `errorMessage(e)` from `apiClient` to toast it, and skip it when `isPlanLimit(e)` — that one is
  already shown by `UpsellModal` and would otherwise stack a toast on top of a modal. The assistant
  was the only place in the app doing this; every other `catch` sets an error state or toasts.
- `ProposalCard` — **editable**: apiary/hive selects plus the per-kind fields; "Potvrdi" stays disabled
  until the target (and, for a todo, the title) is set. When the apiary/hive lists fail to load, the
  card falls back to the names the server sent rather than claiming "Nije određeno". **Phase C**:
  for a resolved existing-record action the apiary/hive selects do not render at all (the target is
  fixed) — instead `DeletePreview` (read-only "what disappears", with an explicit photo-cascade note
  for `DeleteInspection`), `CompletePreview` (read-only, no red styling — not destructive), or
  `UpdateFields` (the same editable inputs as create, seeded from the AI's proposed values, with a
  "prije: X" hint under each field the edit actually changed). An *unresolved* existing-record action
  (`TodoNotFound`/`TodoAmbiguous`/…) shows only the problem text — there is no dropdown fallback for
  "pick a different todo", only the Phase B question/buttons or a more specific retyped command.
- Candidate buttons (Phase B, inside `AssistantThread`) — rendered under the assistant's bubble only
  for the **latest** turn (`turn.id === clarification?.id`), so answering makes them disappear rather
  than staying tappable indefinitely. Tapping one calls the same `send()` a typed message would —
  no separate code path.
- Second confirmation (Phase C, SPEC-17 §7.3) — the "Potvrdi" bar's button opens a `Modal` review
  listing only the destructive selections (via the same `DeletePreview`/new `UpdateSummary`, not a
  second copy of the fields) when any are checked; its own "Da, potvrdi" is what actually calls
  `/confirm`. A create-only batch skips the review and executes on the first click, unchanged from
  Phase A. Built on the shared `Modal` primitive, not `ConfirmDialog` — that dialog's single
  `message: string` prop cannot hold a per-action diff or a delete's full record.
- `AssistantPage` (`/assistant`) — history: every command, the interpretation, and what it created.
  **SPEC-18**: reads a `?beehiveId=` query param (`BeehiveDetailPage`'s **"Pitaj asistenta"** button,
  formerly "Pitaj savjetnika" → `/advisor`) and passes it as `contextBeehiveId` for a fresh session,
  clearing the param once one exists; shows a 🐝 hive chip from the deep link before a session exists
  and from the session's own `beehiveId` afterward — mirroring the retired `AdvisorPage`'s chip exactly.
- Sidebar "AI Asistent" (`Sparkles`), all roles — the former separate "AI Savjetnik" entry and `/advisor`
  route are gone (SPEC-18).
- Help entry under `/assistant` now covers both commands and questions; the old `/advisor` entry is gone.
- `useVoiceInput` reused unchanged. Requests use `timeout: 60_000` to match the backend's Groq budget.

## Tests

`AiEnvelopeParserTests` (junk, unknown kinds, out-of-range enums, loose dates, code fences, bare
numbers, plus Phase C's five new kinds and `targetTitle`), `AiTargetResolverTests` (name matching incl.
diacritics and `đ`, sole-apiary fallback, multi-hive, `"all"`, ambiguity, the ceiling, page context, **an
out-of-scope hive unreachable by any phrasing**, plus Phase C's title/date matching, hive/apiary
narrowing, the "most recent inspection" default, and **a todo outside the supplied pool never
resolves**), `AssistantClarificationBuilderTests` (apiary/hive candidate selection, the 8-button cap,
the `needs`-only fallback, the ceiling short-circuit, plus Phase C's todo/inspection candidates), and
`AiAssistantServiceTests` (404 for non-owners, plan gate before the model, nothing persisted on AI
failure, pre-flight, untrusted confirm payload, per-action partial failure, double-confirm, the
candidate/continuation pipeline, plus Phase C's `IsDestructive`/`PreviousFields` wiring and the
confirm-time target lock, plus **SPEC-18**'s: a zero-action/no-`needs` turn renders as a plain answer
with no cards, context is built only when a hive is in scope, an inaccessible `beehiveId` at session
start is rejected before the model is ever called, and a since-revoked hive on a later turn drops
context silently instead of failing the turn). `AiActionExecutorTests` tests the real executor directly
(not just through the `IAiActionExecutor` mock every other test uses) — field preservation, refusing an
already-completed todo, and a record that changed or vanished between propose and confirm failing
gracefully. `HiveContextBuilderTests` (moved from the advisor's `AdvisorContextBuilderTests` — full/empty
data, 200-char truncation) and a new `AssistantPromptBuilderTests` (the Q&A rule present, the old
advisor deflection gone, the carried-over safety guardrails present, date injection still exact) close a
gap: the prompt's wording was previously untested despite its own doc comment claiming otherwise. Groq
mocked via `IAssistantAiClient` — no network, no database. `BeekeepingPromptTests` locks the
voice-inspection prompt byte-for-byte across the glossary extraction. `PlanGuardTests` covers the merged
`EnsureAiInteractionAsync` (Free blocks, quota exhausted blocks, under quota passes, absent key
unlimited, SystemAdmin bypass) — closing a gap where the assistant's own gate previously had no direct
test, only a mocked `IPlanGuard` in `AiAssistantServiceTests`. 552/552 backend tests green.
