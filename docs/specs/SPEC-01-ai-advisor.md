# SPEC-01 — AI Advisor ("AI savjetnik")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-03). **Superseded 2026-08-09 by SPEC-18** — its Q&A capability merged into AI Asistent and `/advisor` was retired; this document is kept as the historical record of the standalone advisor and is not updated further. |
| **Effort** | M/L (~2–3 days) |
| **Depends on** | nothing (context enrichment from SPEC-02/03 is optional, see §Context) |
| **New secrets** | none — reuses `Groq:ApiKey` |
| **New packages** | none |

## Goal

A beekeeper in the field hits a problem — aggressive colony, suspected disease, queen cells,
robbing — and describes it **by voice or by typing**. The advisor answers in Bosnian with
concrete, practical steps, and (when the question is about a specific hive) grounds the answer
in that hive's **real data**: recent inspections, active feeding, open todos, current weather.

This is a chat: follow-up questions keep context. Conversations are persisted per user.

## User stories

- As a Beekeeper, I open the advisor from a hive page, tap the mic, say
  *"Pčele su jako agresivne i ima dosta matičnjaka, šta da radim?"* and get advice that already
  knows this hive's last inspections.
- As any user, I ask a general question ("Kada se stavlja apistan?") without hive context.
- As a user, I revisit past conversations and continue them.

## UX flow

1. Sidebar entry **"AI Savjetnik"** (`Bot` icon) → `/advisor`: conversation list (left / separate
   screen on mobile) + chat thread.
2. On `BeehiveDetailPage`: button **"Pitaj savjetnika"** → `/advisor?beehiveId={id}` opens a new
   conversation pre-bound to that hive (hive name shown as a chip in the thread header).
3. Input row: textarea + mic button + send. Mic uses the same record flow as inspections
   (`useVoiceInput`): record → transcribe → **transcript lands in the textarea for review** →
   user edits/sends. (Same review-before-commit UX as voice inspections.)
4. Assistant reply renders as plain text with paragraph breaks; a subtle disclaimer footer on the
   thread: *"Savjeti su informativni — za bolesti koje podliježu prijavi obavezno kontaktiraj
   veterinarsku službu."*
5. Conversations are auto-titled from the first user message (first ~60 chars).

## Backend

### Entities (`Melarium.Domain/Entities/`)

```
AdvisorConversation : BaseEntity
  UserId        int        (FK User; owner — conversations are personal, not org-shared)
  BeehiveId     int?       (FK Beehive; null = general question)
  Title         string(80)
  Messages      ICollection<AdvisorMessage>

AdvisorMessage : BaseEntity
  ConversationId int       (FK, cascade delete)
  Role           AdvisorRole enum { User = 1, Assistant = 2 }
  Content        string    (text column)
```

Repository `IAdvisorConversationRepository` (+ impl in `Melarium.Entity/Repositories/`), added to
`IUnitOfWork`: `GetByUserAsync(userId)` (summaries — include last message timestamp via projection,
no full message rows), `GetWithMessagesAsync(id)`. Migration `AddAdvisorConversations`.

### AI client (`Melarium.Application/Features/Advisor/`)

Two small refactors are justified here (structure currently prevents reuse — see `../workflow.md`):

1. **Extract transcription** out of `VoiceParsingService` into
   `Features/Ai/GroqTranscriptionService : ITranscriptionService` (move `TranscribeAsync`,
   the beekeeping `TranscriptionPrompt`, and `GetMimeType` as-is; `VoiceParsingService` consumes
   the interface). No behavior change → record in `decisions.md`.
2. **Introduce `IAdvisorAiClient`** — thin wrapper `SendAsync(IReadOnlyList<ChatMessage>) → string`
   over the Groq chat completions call (same endpoint/auth pattern as `VoiceParsingService`),
   so `AdvisorService` is unit-testable with NSubstitute.

Model: `llama-3.3-70b-versatile`, `temperature 0.4`, `max_tokens 1024` (plain text, **no**
`response_format json`).

### System prompt (Bosnian, fixed)

Core elements — expert beekeeping advisor for Bosnia/region; fluent in BCS terminology (reuse the
glossary block from `VoiceParsingService.SystemMessage`); answers are **practical numbered steps**
where applicable; honest about uncertainty. **Guardrails (must be in the prompt):**

- Nisi veterinar; kod sumnje na američku/europsku gnjiloću naglasi da je bolest **obavezna za
  prijavu** veterinarskoj inspekciji.
- Ne preporučuj doziranje lijekova mimo uputstva proizvođača.
- Ako podaci o košnici (context block) postoje, koristi ih i referiši se na njih; ništa ne izmišljaj.
- Odgovaraj isključivo na bosanskom, sažeto (max ~300 riječi osim ako korisnik traži više).
- Odbij teme koje nisu pčelarstvo (kratko i ljubazno).

### Context block (only when `BeehiveId` is set)

Built by `AdvisorContextBuilder` (pure, unit-testable) as a compact plain-text block appended to the
system prompt, capped at ~1500 tokens:

- Hive: name, type/material labels, apiary name.
- Last **5** inspections: date, honey level (Bosnian label), broodStatus, notes (truncate each to 200 chars).
- Active diet (if any): type, progress (completed/total entries).
- Open todos for the hive (max 5: title, priority, due date).
- Current weather for the apiary via existing `IWeatherService` (**best-effort** — skip silently on
  failure, same policy as inspection temperature auto-fill).
- When SPEC-03 ships: active queen (year, age, status). When SPEC-02 ships: current-season yield kg.
  *(Enrichment only — build the builder so these are added sections, not rewrites.)*

### Conversation policy

- Send to the model: system prompt (+ context block) + **last 12 messages** of the conversation.
- Hard cap **60 messages** per conversation → `BusinessRuleException` ("Započni novi razgovor…").
- Message length 1–4000 chars (FluentValidation).

### Endpoints (`AdvisorController`, `/api/advisor`)

| Method | Path | Body → Returns | Notes |
|---|---|---|---|
| GET | `/conversations` | → `AdvisorConversationSummaryDto[]` | own only; newest first |
| GET | `/conversations/{id}` | → `AdvisorConversationDetailDto` (with messages) | 404 if not owner |
| POST | `/conversations` | `{ beehiveId?, message }` → detail DTO `201` | creates + first AI exchange |
| POST | `/conversations/{id}/messages` | `{ message }` → `AdvisorMessagePairDto` | appends user + assistant msgs |
| POST | `/transcribe` | multipart audio → `{ transcript }` | 15 MB limit, reuses `ITranscriptionService` |
| DELETE | `/conversations/{id}` | → `204` | owner only |

- **Authorization:** ownership enforced in `AdvisorService` (userId from JWT). If `beehiveId` is
  provided, verify access via `IAccessGuard` (same check as viewing the hive).
- **Rate limiting:** new fixed-window policy `ai-chat` **10/min per IP** on both POST message
  endpoints; `/transcribe` joins the existing `parse-voice` policy. Register in `Program.cs`
  alongside the existing policies.
- AI call failures → `502`-style `BusinessRuleException` with Bosnian message ("AI servis trenutno
  nije dostupan…"); the user message is **not persisted** if the AI call fails (transactional: save
  both messages only after a successful AI response).

## Frontend

- `core/models/index.ts`: `AdvisorConversationSummary`, `AdvisorConversationDetail`, `AdvisorMessage`.
- `core/services/advisorService.ts` + hooks in `queries.ts`: `useAdvisorConversations`,
  `useAdvisorConversation(id)`, `useSendAdvisorMessage` (invalidates both keys),
  `useDeleteAdvisorConversation`.
- **Move `useVoiceInput` from `features/inspections/` to `core/hooks/`** and parametrize the upload
  endpoint (inspections keep `parse-voice`, advisor uses `/advisor/transcribe`). Update the
  inspections import — no behavior change.
- `features/advisor/`: `AdvisorPage` (list + thread, responsive), `ChatThread`, `ChatInput`.
  Pending state: disable send, show typing indicator ("Savjetnik piše…"). Failed send: toast, keep
  the text in the input.
- Routes in `App.tsx`: `/advisor` (protected, all roles).
- Sidebar item in `Layout.tsx`: "AI Savjetnik".

## Costs & limits

Groq `llama-3.3-70b-versatile` ≈ $0.59/M input + $0.79/M output tokens (verify current pricing at
implementation). A typical exchange ≈ 2–4k tokens → **well under $0.01**; the 10/min rate limit and
the 12-message window bound worst-case usage.

## Edge cases

- Hive deleted after conversation creation → context builder skips hive data; chip shows "(obrisana)".
  Use `ON DELETE SET NULL` for `BeehiveId`.
- Empty/whitespace transcript from `/transcribe` → `400` with Bosnian message (same as parse-voice).
- Concurrent sends to one conversation: last-write-wins is fine (single user), no locking.

## Out of scope (v1 — do not build)

Streaming responses; tool/function-calling (auto-creating todos from advice); image input
(→ SPEC-05); sharing conversations with org members; markdown rendering in replies.

## Acceptance criteria

**Verified by unit tests + build:**

- [x] User A cannot read/delete user B's conversation (404). *(`AdvisorServiceTests`)*
- [x] AI outage: user message not lost from the input, clear Bosnian error, nothing persisted.
      *(service: `AdvisorServiceTests`; input: `ChatInput` only clears text on a successful send)*
- [x] Unit tests: `AdvisorContextBuilder` (with/without hive, truncation), ownership checks,
      60-message cap — Groq mocked via `IAdvisorAiClient`. *(`AdvisorContextBuilderTests` + `AdvisorServiceTests`)*
- [x] Docs updated: `features/ai-advisor.md`, `api-contracts.md`, `context.md`, `decisions.md`
      (ADR-024), this spec → ✅.

**Implemented; still needs a live check against a running app + Groq (not verifiable in this env):**

- [x] Voice → transcript → edit → send end-to-end on mobile (webm/m4a) — flow built (`useVoiceInput` +
      `/advisor/transcribe` + review-in-textarea); needs a device pass.
- [x] Hive-page question references that hive's facts — grounding wired via `AdvisorContextBuilder`.
- [x] General question + follow-up keeps context — last-12-message window sent each turn.
- [x] AFB-type question triggers the mandatory-reporting warning / non-beekeeping politely declined —
      both are explicit guardrails in the Bosnian system prompt (LLM behavior, spot-check recommended).
- [x] Existing voice inspection flow still works after the transcription extraction — code moved
      verbatim, build + full test suite green; a manual voice-inspection pass is still advised.
