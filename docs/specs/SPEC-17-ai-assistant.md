# SPEC-17 — AI Asistent (naredba → prijedlog → potvrda → radnja)

| | |
|---|---|
| **Status** | ✅ **Implemented 2026-08-09** — all three phases (ADR-033, `docs/features/ai-assistant.md`). **Extended the same day by SPEC-18**, which merges SPEC-01's Q&A capability in here and retires `/advisor` — see that spec for the addition; this document describes Phases A/B/C unchanged. |
| **Effort** | L — Phase A ~4 days, Phase B ~1.5 days, Phase C ~2 days |
| **Depends on** | nothing new. Reuses the Groq stack (SPEC-01: `ITranscriptionService`, the `GroqAdvisorAiClient` pattern), `IAccessGuard`, `IPlanGuard` (SPEC-09), `HiveNumberMatcher` (hive scanning), `AppTimeZone` (SPEC-11), `useVoiceInput` |
| **New secrets / packages** | **none.** `Groq:ApiKey` is reused. Config only: `Groq:AssistantModel`, `Ai:MaxActionsPerCommand`, `Plans:{Plan}:AiCommandsPerMonth` |
| **Breaking** | **No.** Three new tables, three new enums, one new `PlanFeature` value. No existing endpoint changes shape and nothing is removed. |
| **Reserves** | `PlanFeature.AiAssistant = 5` (verified free 2026-08-08: 4 = `PhotoAnalysis`, SPEC-05). No `NotificationType` value needed. |
| **Rate-limit policy** | **none new** — `ai-chat` (10/min) for interpretation, `voice-parse` (10/min) for transcription |

> **How this spec was written.** The product decisions in §1 were settled one at a time with the PO on
> 2026-08-08, against the code as it stands on `main` that day, before any of this was drafted. This
> document is the **record** of those decisions, not the place where they were made. Where a decision
> closed off an option, the option is named so a future reader knows it was considered.
>
> Every claim about existing code here was verified on 2026-08-08. This codebase moves week to week
> (SPEC-12 shipped 2026-08-05, SPEC-15 Phase 1 on 2026-08-06) — **re-check the cited lines before
> implementing**, and treat a mismatch as the code being right and this document being stale.

---

## 0. Why this exists

Melarium already has two AI surfaces, and neither does this:

- **AI Savjetnik** (`/advisor`, SPEC-01) *answers questions*. It reads hive data; it writes nothing.
- **Glasovni unos pregleda** (`VoiceParsingService`) *fills one form*, on one hive the user has already
  navigated to, with four fixed fields.

Nothing turns a spoken sentence into an **action anywhere in the app**. To log an inspection today a
beekeeper opens the apiary, finds the hive, opens the form, records, confirms — five steps, each
requiring both hands and a look at the screen, while wearing gloves and holding a smoker.

The goal is that the sentence the beekeeper would say anyway —

> *"Pregledana košnica 2 na pčelinjaku Zlatna dolina, u toj košnici imamo 5 ramova legla, nivo meda je
> zadovoljavajući, i izvršiti pregled za 10 dana."*

— finds the apiary and the hive itself, fills the inspection, **also** proposes a todo due in 10 days,
and shows all of it for confirmation before anything touches the database.

---

## 1. What was decided

| # | Decision | |
|---|---|---|
| D1 | **Actions in v1** | **Pregled** (inspection) and **Zadatak** (todo). Vrcanje, tretman, prehrana and matica are **not** in this spec — each is its own resolver, prompt, card and test set. |
| D2 | **Entry point** | A **separate assistant**: a floating mic button on every page (the scan precedent) plus `/assistant` for history. The advisor stays the advisor. Merging the two into one bot was considered and rejected — a router that has to guess "question or command" gets it wrong, and a chat bubble is a bad place for an editable form. |
| D3 | **Several actions per request** | **Yes, one confirmation for all.** *"…i pregled za 10 dana"* → inspection **plus** a todo dated +10 days. Individual cards can be unchecked. Rationale: "sljedeći pregled za X dana" is the most common sentence on an apiary, and as a note inside an old inspection nobody ever sees it again; as a todo it reaches the calendar and the reminders. |
| D4 | **Plans** | **Standard+**, with a monthly per-organization quota. Free → 402 → the existing upsell modal. |
| D5 | **Scope over data** | **Full**: create, update, delete — but **update and delete require a second, separate confirmation** (§7.3). |
| D6 | **Number of hives** | **Arbitrary**: one, an enumerated few (*"1, 2 i 3"*), or **all** (*"sve košnice na Zlatnoj dolini"*). |
| D7 | **Ambiguity** | **Both mechanisms, in this order.** Everything resolved → the card appears immediately, with no needless question. Something missing → a short follow-up question in the thread with the candidates as tappable buttons. The card is editable either way, so a wrongly-guessed field is fixed with one tap, not another round of conversation. |
| D8 | **History** | **Everything**: the original text/transcript, what the AI understood, and what was created, linked to the record. |
| D9 | **Phase order** | A: foundation + creation → B: conversation → C: update and delete. Each ships alone. |

**The invariant that outranks everything else in this spec:**

> **Nothing is written without an explicit human confirmation, and the assistant can never target a
> record the user could not already reach.** Name resolution searches **only** within the caller's
> accessible apiaries and hives, so an out-of-scope hive is unreachable *by construction* — not by a
> check somebody could forget to add to the next action kind.

---

## 2. The flow

```
voice ──► POST /assistant/transcribe   (Whisper, shared ITranscriptionService)
                     │
text ────────────────┴──► POST /assistant/sessions  ·  /sessions/{id}/turns
                                  │
                    Groq Llama (json_object, temp 0) ──► envelope (§3)
                                  │
                          AiTargetResolver (pure, §4)
                    ┌─────────────┴─────────────┐
              all resolved                 something missing
                    │                             │
           proposal cards (Pending)      question + candidate buttons   ← Phase B
                    │                             │
                    └──────────► CONFIRMATION ◄───┘
                                  │
                    POST /assistant/turns/{id}/confirm
                                  │
                    AiActionExecutor → the existing services (§5)
                                  │
                       per-action result + links to the records
```

---

## 3. What the model returns

One JSON envelope per turn, `response_format: { type: "json_object" }`, `temperature: 0`, model from
`Groq:AssistantModel` (default `llama-3.3-70b-versatile` — the model every other Groq text feature in
this app already uses).

> **2026-08-17:** Groq retired `llama-3.3-70b-versatile` and `Groq:AssistantModel` was folded into a
> single `Groq:ChatModel` shared by all four Groq text features (ADR-035). The `response_format`
> requirement above is unchanged and now constrains what may replace it.

```json
{
  "reply": "Unosim pregled za Košnicu 2 na pčelinjaku Zlatna dolina i zadatak za 18.08.",
  "needs": null,
  "actions": [
    { "kind": "create_inspection",
      "apiary": "Zlatna dolina", "hives": ["2"],
      "fields": { "date": "2026-08-08", "honeyLevel": 2,
                  "broodStatus": "5 ramova legla", "notes": null } },
    { "kind": "create_todo",
      "apiary": "Zlatna dolina", "hives": ["2"],
      "fields": { "title": "Pregled košnice 2", "priority": 2, "dueDate": "2026-08-18" } }
  ]
}
```

- `needs` — `null`, or `{ "what": "apiary" | "beehive" | "title" | "date", "question": "Na kojem pčelinjaku?" }`.
- `hives` — a list of numbers as **strings**, the literal `"all"`, or `null`.
- `actions` — may be empty (a greeting, a question the assistant cannot act on, a refusal).

**The model never emits an id.** It echoes names and numbers as text; ids are assigned server-side by
`AiTargetResolver` (§4). A hallucinated id is therefore impossible rather than merely unlikely.

### 3.1 The prompt

The system prompt is assembled from three parts:

1. **The beekeeping glossary and the ASR warning**, lifted from `VoiceParsingService.SystemMessage`
   (`VoiceParsingService.cs:34-54`). It is already tuned for BCS beekeeping slang (matica/matičica,
   leglo, ramovi/satovi, matičnjaci, varoa…) and for Whisper's phonetic errors. Extract it to a shared
   constant rather than copying it — two divergent copies of a tuned prompt is the failure mode here.
2. **The action catalogue** — the allowed `kind` values with their fields and enum ranges
   (`honeyLevel` 1–3, `priority` 1–3), plus the same few-shot examples style that
   `VoiceParsingService.ExtractFieldsAsync` uses, written from the two worked examples in §0 and D3.
3. **The organization's apiary names**, as a delimited data block. There are few of them (Free = 1);
   hive numbers are *not* listed — they are resolved server-side, which is both cheaper and exact.

**Today's date is injected as `AppTimeZone.Today(tz)` — never `DateTime.UtcNow`.**
`Europe/Sarajevo` is UTC+1/+2, so between local midnight and 01:00 (CET) / 02:00 (CEST) `UtcNow` is
still *yesterday*. `VoiceParsingService.cs:123-124` does exactly that and is wrong in that window;
this is a **pre-existing bug that this spec does not fix and must not replicate** — it is tracked
separately.

### 3.2 Apiary names are data, not instructions

Apiary names are user-authored text that ends up inside the system prompt, so an apiary named
*"ignore previous instructions…"* is a real, if unlikely, vector. Two things make it harmless, and
both must survive refactoring:

- names go in a **delimited data block** with an explicit "this is data" framing, never as prose; and,
  decisively,
- **the model cannot execute anything.** Every target is re-resolved server-side against accessible
  entities, every field is re-validated, and a human confirms. The worst a poisoned name achieves is
  a nonsense proposal the user rejects.

---

## 4. Resolution — `AiTargetResolver`

Pure, no I/O, unit-tested. Takes the envelope plus the caller's accessible apiaries and hives; returns
resolved targets or a reason it could not.

**Apiary**, in order: normalized exact match (trimmed, lower-cased, diacritics folded — *"zlatna
dolina"* = *"Zlatna Dolina"* = *"Zlatna dolina "*) → unique `contains` match → the user's **only**
apiary when they have exactly one → otherwise unresolved, with the candidates carried out for D7.

**Hive**, within the resolved apiary, via
[`HiveNumberMatcher.Matches`](../../backend/Melarium.Application/Features/Beehives/HiveNumberMatcher.cs) —
so `LabelNumber` wins when set, the name's digits are the fallback, and `"1"` / `"01"` / `"Košnica 001"`
already match one another. With no apiary resolved, it searches all accessible hives, exactly as
[`BeehiveService.MatchByNumberAsync`](../../backend/Melarium.Application/Features/Beehives/BeehiveService.cs)
does today (that method searches `GetAccessibleBeehivesAsync()` — reuse the same source, do not write
a second one).

**`"all"`** → every accessible hive in the resolved apiary. Requires a resolved apiary; *"sve košnice"*
with no apiary and more than one apiary is an ambiguity, not a wildcard over the whole organization.

**Page context.** `apiaryId` / `beehiveId` from the route the user is standing on fill **gaps only**.
An explicitly spoken name always wins — standing on hive 5 and saying *"pregled za košnicu 2"* means
hive 2.

**Batch ceiling.** `Ai:MaxActionsPerCommand` (default 50). Above it nothing executes; the card states
how many records were matched and asks the user to narrow the command. A card that resolved through
`"all"` **always** spells out the count before confirmation:
*"Unosim pregled za svih 37 košnica na pčelinjaku Zlatna dolina."*

---

## 5. Execution — the contract with the rest of the app

### 5.1 The executor calls the existing services, never repositories

`AiActionExecutor` builds `CreateInspectionDto` / `CreateTodoDto` — **the same DTOs the forms post** —
and calls `InspectionService.CreateAsync` / `TodoService.CreateAsync`. That is the whole design. It
buys, for free and without a second implementation to keep in sync:

- `IAccessGuard` on every target (`InspectionService.cs:63`, `TodoService.cs:68-81`);
- `IPlanGuard` limits;
- automatic temperature from the apiary's weather (`InspectionService.cs:69`);
- the todo notification cascade to superiors and assignees (`TodoService.cs:229`).

Writing to repositories directly would silently drop all four. This is the one rule in this spec that
must not be "optimized" later.

### 5.2 The executor runs the same validators the controllers run

In this codebase FluentValidation lives in the **controllers**, not the services (see any
`Create*` action, e.g. `AdvisorController.cs:63`). Without this step, data authored by the AI would
pass **fewer** checks than data typed by a human. The executor resolves
`IValidator<CreateInspectionDto>` / `IValidator<CreateTodoDto>` from DI and validates before calling
the service.

### 5.3 The confirm payload is untrusted input

The card is editable, so the confirm request carries the (possibly edited) values — including
`beehiveId`. **The client can send any id.** The proposal is not a capability grant: the confirm path
re-runs resolution, validation and access checks exactly as if the values had been typed into a form.
§5.1 is what makes this true by default; do not add a "we already checked at propose time" shortcut.

### 5.4 Partial success is reported, not rolled back

Each service calls `SaveChangesAsync()` itself. With a shared `DbContext`, a failure on action 3
cannot cleanly undo actions 1 and 2 — the same trap SPEC-15 §3.2 documents. Therefore:

- **pre-flight at propose time** — resolve, validate and access-check *before* a card is ever shown, so
  a doomed action never becomes confirmable; and
- **execute in order, record a status per action.** The result says exactly what landed and what did
  not. Never report a batch as successful when it partly was.

### 5.5 Double-confirm guard

A double-tap on a phone must not create two inspections. Confirmation is a `Pending → Confirmed`
transition on the action rows; a turn whose actions are no longer all `Pending` throws
`BusinessRuleException` ("Ovaj prijedlog je već obrađen.") rather than executing again.

### 5.6 Transactional AI

Following `AdvisorService`: an AI failure throws `BusinessRuleException` ("AI servis trenutno nije
dostupan…") and **persists nothing**, so the user's text is never lost to a Groq outage.

---

## 6. What is stored

Three tables, one migration `AddAiAssistant`. Shapes mirror `AdvisorConversation` / `AdvisorMessage`
(`Configurations/AdvisorConversationConfiguration.cs` is the template).

| Entity | Columns |
|---|---|
| `AiAssistantSession` | `UserId` (owner, **cascade**), `Title` (auto from the first message, ~60 chars), `Turns` |
| `AiAssistantTurn` | `SessionId`, `Role` (`AiTurnRole`), `Content`, `Transcript?`, `RawModelJson?`, `CandidatesJson?` (Phase B) |
| `AiAssistantAction` | `TurnId`, `Kind`, `PayloadJson`, `TargetSummary`, `Status`, `ResultEntityType?`, `ResultEntityId?`, `ErrorMessage?` |

New enums: `AiTurnRole { User = 1, Assistant = 2 }`, `AiActionKind { CreateInspection = 1, CreateTodo = 2 }`
(3–7 reserved for Phase C, §9), `AiActionStatus { Pending = 1, Confirmed = 2, Rejected = 3, Failed = 4 }`.

**Why `AiTurnRole` and not the existing `AdvisorRole`.** The values are identical today. A shared enum
would mean the advisor gaining a `System` role silently hands it to the assistant, and
`AiAssistantTurn.Role` typed as `AdvisorRole` reads as a mistake. One file is cheaper than the coupling.

`ResultEntityType` / `ResultEntityId` are the audit trail D8 asks for and the link target on the
history page. **No FK** — an inspection deleted later must not delete the record that it was created
by the assistant.

**One repository**, `IAiAssistantSessionRepository`, with `GetByUserAsync` (summaries, ordered by
`UpdatedAt ?? CreatedAt`) and `GetWithTurnsAsync` (tracked, for appends) — the two methods
`IAdvisorConversationRepository` already has, for the same two reasons.

---

## 7. Surface

### 7.1 API — `AssistantController`, `api/assistant`, `[Authorize]` at class level

| Method | Path | Notes |
|---|---|---|
| GET | `/sessions` | own summaries, newest activity first |
| GET | `/sessions/{id}` | thread with actions and their statuses; **404 if not the owner** (never 403 — no existence oracle, per `AdvisorService`) |
| POST | `/sessions` | `{ text, apiaryId?, beehiveId? }` → 201 thread with proposals; `ai-chat` |
| POST | `/sessions/{id}/turns` | continues the thread; `ai-chat` |
| POST | `/turns/{id}/confirm` | `{ actions: [{ id, beehiveId?, fields }] }` → per-action result; unlisted actions become `Rejected` |
| POST | `/turns/{id}/reject` | 204 |
| POST | `/transcribe` | multipart → `{ transcript }`; `voice-parse`, 15 MB cap |
| DELETE | `/sessions/{id}` | 204 (owner only) |

Session cap **40 turns** and message length 1–4000, mirroring the advisor's 60-message cap and its
`SendMessageValidator`.

### 7.2 Frontend

`features/assistant/`:

- `AssistantLauncher.tsx` — the floating mic button, mounted once in `Layout.tsx`; hidden offline.
  *(Renamed `AssistantSheet.tsx` on 2026-08-17: the button moved into `shared/components/FabDock`,
  which `Layout` owns, so the assistant and the QR scanner stop overlapping on a phone.)*
- `AssistantSheet.tsx` — the thread: input (mic + text), messages, cards.
- `ProposalCard.tsx` — **editable**. Apiary and hive are selects populated from the entities the user
  can reach; an unresolved field is required and keeps "Potvrdi" disabled.
- `AssistantPage.tsx` — `/assistant`, history (D8), each executed action linking to its record.

`core/services/assistantService.ts` + `assistantQueries.ts`. Sidebar item **"AI Asistent"**
(`Sparkles`), route for all roles.

Two things copied deliberately from existing code:

- **`timeout: 60_000`** on the interpretation calls. `apiClient`'s 10 s default aborts the request
  while the server keeps going — the answer gets persisted and billed but shown as an error. This is
  documented in `advisorService.ts` because it already happened once.
- 402 becomes the upsell modal automatically through the interceptor at
  `frontend/src/core/services/apiClient.ts:33`. **No new frontend work for plan gating.**

**Offline:** the assistant is locked (transcription *and* parsing are server-side), matching how
SPEC-07 treats voice input. `useVoiceInput` is reused unchanged.

**Phase B addition:** the thread (`AssistantThread.tsx` — the file the plan above calls
`AssistantSheet.tsx` was built under this name instead) renders a row of rounded buttons under the
assistant's bubble whenever the **latest** turn carries candidates. Tapping one calls the same `send()`
path as typing — no separate code path to keep in sync with the editable card.

### 7.3 Confirmation, and the second confirmation (D5)

One "Potvrdi" commits every checked card. But if the batch contains **any** update or delete
(Phase C), that button opens a **second** modal listing only those, requiring its own confirmation.
An update card shows **old → new**; a delete card shows the whole record that disappears. Deleting an
inspection also removes its photos through the FK cascade (`InspectionService.cs:110-127`) — the modal
must say so out loud.

---

## 8. Plans and limits

`PlanFeature.AiAssistant = 5`, plus `PlanGuard.EnsureAiCommandAsync(orgId)` built exactly like
`EnsureAdvisorMessageAsync` (`PlanGuard.cs:87-102`): Free throws `PlanLimitException`, otherwise the
monthly count of the organization's user turns in the current UTC calendar month is compared against
`Plans:{Plan}:AiCommandsPerMonth` (absent = unlimited, the established convention). `GetMyPlanAsync`
gains the matching usage line so `/plans` can display it.

The quota is charged on **interpretation**, not on confirmation — that is where the Groq call is. A
rejected proposal still costs a command, and that is correct: it consumed the model.

---

## 9. Phases

**Phase A — foundation and creation.** Three entities + three enums, the `AddAiAssistant` migration,
repository + `IUnitOfWork`, `IAssistantAiClient`/`GroqAssistantAiClient`, `AiEnvelopeParser`,
`AiTargetResolver`, `AiActionExecutor`, `AiAssistantService`, `AssistantController`,
`PlanFeature.AiAssistant` + `EnsureAiCommandAsync`, the launcher, the sheet, the editable card,
`/assistant`, sidebar + route, help entries, and the tests below.

Ships alone and is fully usable: both worked examples in §0/D3 work end to end, including several
hives and `"all"`. Ambiguity is handled by the card's required dropdown — the conversational path is
Phase B, and its absence degrades one tap, not the feature.

**Phase B — conversation. Implemented 2026-08-08.** A new pure class, `AssistantClarificationBuilder`,
turns an unresolved action's candidates (or, absent those, the model's own `needs` field against the
full accessible set) into tappable buttons — capped at 8, so a large organization degrades to the
question text plus the card's dropdown rather than a button wall. Candidates are persisted per turn
(`CandidatesJson`) so re-opening a session reproduces the same buttons, and only the **latest** assistant
turn keeps them — `AiAssistantService.ToDetail` zeroes out every earlier one, so an answered question
can never be tapped twice. Tapping a button sends its text as an ordinary new turn — no shortcut around
interpretation — and the previous thread goes to the model as context (**last 8 turns**, the advisor's
"last 12 messages" precedent scaled to turns that carry JSON). The system prompt gained an explicit
continuation rule (§3.1) telling the model to combine a short follow-up with the fields the beekeeper
already dictated, rather than treating it as an isolated new command. The card stays editable throughout:
the question is the faster path, not the only one.

**Phase C — update and delete. Implemented 2026-08-09.** `AiActionKind` extends to `UpdateTodo = 3,
CompleteTodo = 4, UpdateInspection = 5, DeleteTodo = 6, DeleteInspection = 7`, calling `UpdateAsync` /
`DeleteAsync` on the same services — no new repository code, no new validators (`UpdateTodoDto` /
`UpdateInspectionDto` and their FluentValidation validators already existed for the edit forms).

**A resolver for existing records**, reusing the pure-function split the rest of the pipeline already
uses: `AiTargetResolver` gained `ResolveExistingTodo` (match by title, normalized the same way an
apiary name is, narrowed by a named hive/apiary when given) and `ResolveExistingInspection` (match by
date within one resolved hive; no date named = the most recent inspection — "zadnji pregled" is the
natural reading, not an ambiguity across the hive's whole history). Both search only
`AiResolutionContext.Todos`/`Inspections`, which `AiAssistantService` populates **only when the
envelope actually contains an action that needs them** — `ITodoService.GetAllOpenForCurrentUserAsync()`
for todos (already role-scoped, the exact same guarantee `IAccessGuard` gives the apiary/hive lists),
`IInspectionService.GetByBeehiveIdAsync` per hive for inspections, found via a lightweight pre-scan
with the same `HiveNumberMatcher` the resolver itself uses. Ambiguity (two todos sharing a title, two
inspections on the same date) produces candidates in the exact same shape Phase B already renders as
buttons — `AssistantClarificationBuilder` needed one more branch, not a parallel mechanism.

**One deliberate narrowing of D5.** `CompleteTodo` is its own `AiActionKind`, separate from
`UpdateTodo`, and it turned out **not** to need the second confirmation: D5 says "update and delete",
and checking a todo off is a one-tap, instantly-reversible toggle everywhere else in this app already
(the plain todo checkbox asks nothing). Holding the assistant to a stricter bar than the rest of the UI
already applies to the identical action would be inventing risk that is not there. `IsDestructive` on
`AssistantActionDto` is computed from four kinds, not five.

**The existing-record target is fixed, not re-pickable.** A create action's card lets the user swap
in a different apiary/hive from a dropdown; an update/delete card does not — there is no "pick a
different todo" dropdown by design (§5.3's confirm-time re-check still applies: the executor re-fetches
the record and re-runs the service's own access check, so this is not a shortcut, just not a second
target-selection UI). The confirm request's `apiaryId`/`beehiveId` are therefore ignored for these five
kinds; only the edited fields flow through.

Adds the second-confirmation modal (§7.3), built on the existing `Modal` primitive — not a new
component from scratch, and not `ConfirmDialog` either, since that dialog's single `message: string`
prop cannot hold a per-action old→new diff or a delete's full record. `ProposalCard.tsx` exports
`DeletePreview` and `UpdateSummary` so the modal shows the **exact same** read-only content the card
already renders, rather than a second, driftable copy of the same fields.

**Why this order.** Phase A is whole on its own, and Phase C — the only part that can overwrite or
destroy a correct record — arrives after the prompt and the resolver have met real usage. Shipping C
first would put the destructive path in front of the evidence about where the model actually errs.

### Tests — part of Phase A, not a follow-up

All pure, no database, following `AdvisorServiceTests` (Groq mocked through `IAssistantAiClient`):

- `AiTargetResolverTests` — exact / diacritic-folded / `contains` apiary match; the sole-apiary
  fallback; hive by `LabelNumber` and by name digits; `"1"` vs `"01"`; `"all"`; two apiaries with a
  hive 2 → ambiguous, not a guess; **a hive outside the caller's access never resolves**; the
  `MaxActionsPerCommand` ceiling; page context fills gaps but never overrides a spoken name.
- `AiEnvelopeParserTests` — malformed JSON, unknown `kind`, missing/`null` fields, out-of-range enum
  values, and relative dates resolved in `Europe/Sarajevo` **including the 00:30 local case** that
  catches a `UtcNow` regression.
- `AiAssistantServiceTests` — another user's session → 404; the plan quota; an AI failure persists
  nothing; pre-flight rejects an inaccessible target; a mid-batch failure is reported per action with
  the earlier ones intact; a second confirm on the same turn throws instead of duplicating.

---

## 10. Outcomes considered

| Situation | What happens |
|---|---|
| Both worked examples from §0/D3 | Inspection + todo, one confirmation, individually uncheckable |
| Apiary named but not found | Card appears with a required apiary dropdown (D7); nothing spoken is lost |
| Two apiaries both have a "košnica 2" | Ambiguous → question (Phase B) / dropdown (Phase A). **Never a guess** |
| User has exactly one apiary and names no apiary | Resolved to it |
| *"sve košnice na Zlatnoj dolini"* | All accessible hives there; the count is spelled out before confirming |
| *"sve košnice"* with several apiaries | Ambiguous — not a wildcard over the organization |
| Batch exceeds `MaxActionsPerCommand` | Nothing executes; the card says how many matched and asks to narrow |
| Beekeeper names a hive not assigned to them | **Does not resolve.** Not a 403 — it is not in the search space (§1 invariant) |
| Beekeeper asks for an apiary-level todo | `TodoService` requires `EnsureCanManageApiaryAsync` — caught at pre-flight, so the card never appears |
| Client posts an edited `beehiveId` it should not have | Re-checked by `IAccessGuard` on the confirm path (§5.3) |
| User double-taps "Potvrdi" | Second call throws; one inspection (§5.5) |
| Groq is down / returns junk | `BusinessRuleException`, **nothing persisted**, the typed text survives |
| One action of three fails | The other two stand; the result names the failure (§5.4) |
| Plan limit hit halfway through a batch | Same as above — honest partial result, not a fake success |
| Free plan | 402 → the existing upsell modal, zero new frontend code |
| Offline | Launcher locked, like voice input in SPEC-07 |
| Recording at 00:30 local | "danas" is today, because dates come from `AppTimeZone` (§3.1) |
| Apiary named to look like a prompt injection | Data block + server-side resolution + human confirmation (§3.2) |
| Model proposes an unknown action kind | Dropped by the parser; the rest of the envelope still works |
| User deletes the created inspection later | The history row survives — no FK by design (§6) |
| Session owner is deleted | Cascade; the sessions go with the user |
| Two todos share a title | `TodoAmbiguous` → candidates labelled by hive/apiary, since the title alone won't disambiguate a retyped answer |
| "Završi zadatak X" where X does not exist | `TodoNotFound` — the reply says so; no dropdown fallback exists for an existing-record miss |
| "Izmijeni pregled košnice 2" with no date | Resolves to the most recent inspection of that hive — "zadnji pregled" is read literally |
| Two inspections share the named date | `InspectionAmbiguous` → candidates, never a guess at which one |
| A batch mixes a create and a delete | One "Potvrdi" reviews all; the second modal lists only the delete |
| A create-only batch | No second modal — straight to execution, same as Phase A |
| User checks a todo off via the assistant | `CompleteTodo`, **not** treated as destructive — one tap, no second confirmation, matching the plain todo checkbox elsewhere in the app |
| Confirm request tries to redirect an update to a different hive | Ignored — the existing record's id came from propose-time resolution, not the client (§5.3) |
| Deleting an inspection that has photos | The card and the second modal both say so; `InspectionService.DeleteAsync`'s existing cascade does the rest |

---

## 11. Acceptance criteria

**Phase A — locked by automated tests:**

- [ ] `AiTargetResolverTests`, `AiEnvelopeParserTests` and `AiAssistantServiceTests` cover every case listed in §9.
- [ ] A hive outside the caller's access **never resolves**, for every role in `AccessGuardTests`' matrix.
- [ ] A second confirm on the same turn throws rather than duplicating the records.
- [ ] An AI failure leaves no `AiAssistantSession`, no turn and no action row.
- [ ] Relative dates resolve in `Europe/Sarajevo`, asserted at 00:30 local.

**Phase A — verified through the app** (needs a database and a signed-in session):

- [ ] Example 1 produces **two** cards — an inspection on Zlatna dolina / Košnica 2 with med *srednji* and leglo *5 ramova*, and a todo due +10 days.
- [ ] Unchecking the todo card and confirming creates **only** the inspection.
- [ ] Example 2 creates a todo with priority *srednji* and the correct due date, and it appears in the Calendar.
- [ ] The same sentences spoken into the mic produce the same cards.
- [ ] *"košnice 1, 2 i 3"* → three cards; *"sve košnice na X"* → one card stating the count.
- [ ] An unknown apiary yields a card whose "Potvrdi" is disabled until the dropdown is set.
- [ ] Signed in as a Beekeeper, a hive not assigned to them cannot be reached by any phrasing.
- [ ] A Free organization gets 402 and the upsell modal; a Standard organization's usage shows on `/plans`.
- [ ] With the network off, the launcher is disabled.
- [ ] `/assistant` shows the original text, the interpretation and links to the created records.

**Phase A — done:**

- [ ] All user-facing strings Bosnian; `/assistant` has a `helpRoutes` **and** a `helpContent` entry.
- [ ] The nav item is added to `getNavItems` (`Sidebar.tsx:28`) — one entry, because that list feeds the desktop sidebar (`Sidebar.tsx:86`) *and* the mobile panel (`Layout.tsx:309`). The **launcher** has no such shared source, so it is checked separately on both layouts.
- [ ] Docs: `features/ai-assistant.md`, `api-contracts.md`, `context.md`, `decisions.md` (**ADR-033**), `.env.example`, this spec set to ✅.

**Phase B — locked by automated tests** (`AssistantClarificationBuilderTests`, plus the clarification
tests in `AiAssistantServiceTests`):

- [x] A missing apiary produces a question with the candidates as buttons, answerable by tap or voice.
- [x] The answer continues the same session and the resulting card carries the earlier fields.
- [x] A fully-resolved command still produces **no** question.
- [x] Only the latest assistant turn keeps its candidates once a newer turn exists.
- [x] An exceeded ceiling never offers buttons, even when an action would otherwise qualify.

**Phase B — verified in the browser** (dev server, stubbed API — no backend, per the note below):

- [x] An unresolved apiary renders as a question bubble with tappable buttons for each reachable apiary.
- [x] Tapping a candidate sends its text as the next turn; the old buttons disappear and the resulting
      turn shows a resolved proposal card with the apiary/hive pre-selected.

**Phase C — locked by automated tests** (`AiTargetResolverTests`, `AssistantClarificationBuilderTests`,
the new `AiActionExecutorTests` — direct tests of the real executor, closing a gap Phase A left since
every earlier assistant test mocked `IAiActionExecutor` instead — and `AiAssistantServiceTests`):

- [x] Update and delete work for inspections and todos: title/date matching, hive/apiary narrowing,
      ambiguity with candidates, "most recent inspection" default, not-found.
- [x] `UpdateTodoDto`/`UpdateInspectionDto` preserve every field the AI did not mention — asserted by
      capturing the DTO the executor actually sends, not just checking the outcome.
- [x] `CompleteTodo` refuses an already-completed todo without calling `UpdateAsync`.
- [x] A record that changed or became inaccessible between propose and confirm fails gracefully
      (the service's own exception, not an unhandled one) — proven directly against the executor.
- [x] `IsDestructive` is `true` for update/delete, `false` for create **and** for `CompleteTodo`.
- [x] The confirm request's `apiaryId`/`beehiveId` cannot redirect an update/delete to a different
      record — the resolved `ExistingEntityId` from propose time is what executes.
- [x] The todo/inspection search pool is fetched only when an action actually needs it.

**Phase C — verified in the browser** (dev server, stubbed API — no backend, per the note in §11):

- [x] A resolved `DeleteTodo` card shows "Ovo će trajno obrisati:" with the todo's current title,
      priority, due date and notes, read-only.
- [x] The first "Potvrdi" click opens the second modal **without** calling `/confirm`; "Da, potvrdi"
      inside it calls `/confirm` exactly once and closes the modal.

**Phase C — not verified against a live model:**

- [ ] The prompt's rule 9 (create vs. update/complete/delete by verb) and the `targetTitle`/date
      extraction have not been exercised against real Groq output — only against hand-written envelopes
      in tests, per the same limitation noted for Phases A and B.
- [ ] The update card's old → new field hints, rendered but not screenshotted in this pass.

---

## 12. Deliberately out of scope

Named so they read as decisions, not omissions: **other entities** — vrcanje, tretman, prehrana,
matica, troškovi (D1); **read-only questions over data** (*"koliko sam meda izvrcao?"*) — that is the
advisor's job, not a second answer path; **navigation commands** (*"otvori košnicu 3"*); **offline
queueing** of commands into the SPEC-07 outbox; **wake-word / hands-free listening**; **per-user
timezones** (`AppTimeZone` is app-wide by design); **assistant actions on behalf of another user**;
**scheduled or recurring commands**; **bulk update/delete** over several existing records in one
action (§4/§9's resolvers each return at most one existing target — "obriši sve zadatke o postolju" is
not supported); **moving an inspection to a different hive or date** via update (§9 Phase C: only
honeyLevel/broodStatus/notes are editable new values; the hive and date identify the record).

**Open item — what the transcripts contain.** D8 stores the raw transcript, and a recording made on an
apiary picks up whatever else was said near the microphone. Every other table in this app stores data
someone deliberately typed. There is no deletion or export machinery to hang a retention rule on — the
GDPR work is the postponed Phase 4 of the July 2026 refactor, and SPEC-16 is the retention spec.

The v1 mitigation is real but partial: sessions are **private to their owner** (another user's session
is a 404, not a 403), and deleting a session deletes its turns.

**The decision still to take**, alongside SPEC-16 rather than inside this spec: whether transcripts get
a hard expiry — 12 months is the obvious default — and whether the assistant offers a "ne čuvaj
transkript" toggle. Recorded here so it stays a scheduled decision rather than a later discovery.
