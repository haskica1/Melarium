# Feature: AI Advisor (AI Savjetnik)

## Overview

A Bosnian-language beekeeping chat advisor. The user describes a problem (aggressive colony, suspected
disease, queen cells, robbing…) **by voice or text** and gets practical, numbered advice. When a
conversation is bound to a hive, answers are grounded in that hive's **real data** (recent inspections,
active feeding, open todos, active queen, season yield, latest treatment/karenca, current weather). Conversations are personal
and persisted. Implemented per [SPEC-01](../specs/SPEC-01-ai-advisor.md); reuses the Groq stack — no new
provider or secret.

## AI clients (`Melarium.Application/Features/Ai/`, ADR-024)

- `ITranscriptionService` / `GroqTranscriptionService` — Whisper large-v3 (BCS). **Extracted** from
  `VoiceParsingService` so transcription is shared by voice inspections and the advisor's `/transcribe`.
  `VoiceParsingService` now consumes the interface (no behavior change).
- `IAdvisorAiClient` / `GroqAdvisorAiClient` — thin wrapper over Groq chat completions
  (`llama-3.3-70b-versatile`, temp 0.4, max_tokens 1024, plain text). Behind an interface so
  `AdvisorService` is unit-testable with a mocked client.

## Domain (`AdvisorConversation`, `AdvisorMessage`)

- `AdvisorConversation`: `UserId` (owner, cascade), `BeehiveId?` (**ON DELETE SET NULL** — a deleted
  hive detaches; the chip shows "(obrisana)"), `Title` (auto from first message, ~60 chars), `Messages`.
- `AdvisorMessage`: `Role` (`User`/`Assistant`), `Content` (text). Migration `AddAdvisorConversations`.
- `IAdvisorConversationRepository`: `GetByUserAsync` (summaries, ordered by `UpdatedAt ?? CreatedAt`
  which is bumped on each exchange), `GetWithMessagesAsync` (tracked, for appends).

## Service rules (`AdvisorService`)

- **Ownership** from `ICurrentUser`; a conversation not owned by the caller returns **404** (never leaks
  existence).
- **Grounding:** when `BeehiveId` is set, `AdvisorContextBuilder` (pure, unit-tested) assembles a compact
  plain-text hive block appended to the system prompt. Weather is best-effort (skipped silently on
  failure). On create, hive access is verified via `IAccessGuard.EnsureCanAccessBeehiveAsync`; on each
  send it re-checks non-throwing and just drops context if access was revoked.
- **Conversation policy:** system prompt (+ context) + **last 12** messages sent to the model; hard cap
  **60 messages** per conversation → `BusinessRuleException`. Message length 1–4000 (FluentValidation).
- **Transactional AI:** the user + assistant messages are saved **only after** a successful reply. An AI
  failure throws `BusinessRuleException` ("AI servis trenutno nije dostupan…") and persists nothing, so
  the user's text is never lost.
- **System prompt guardrails (Bosnian):** not a vet — AFB/EFB (američka/europska gnjiloća) flagged as
  **obavezna prijava** to the veterinary inspection; no dosing beyond manufacturer instructions; use the
  context block, invent nothing; answer only in Bosnian, ~300 words; politely decline non-beekeeping.

## API (`/api/advisor`)

| Method | Path | Notes |
|---|---|---|
| GET | `/conversations` | own summaries, newest activity first |
| GET | `/conversations/{id}` | detail with messages (404 if not owner) |
| POST | `/conversations` | `{ beehiveId?, message }` → 201 detail (creates + first exchange); `ai-chat` 10/min |
| POST | `/conversations/{id}/messages` | `{ message }` → user+assistant pair; `ai-chat` 10/min |
| POST | `/transcribe` | multipart audio → `{ transcript }`; `voice-parse` policy, 15 MB cap |
| DELETE | `/conversations/{id}` | 204 (owner only) |

## Frontend

- `useVoiceInput` **moved** to `core/hooks/` (record → Blob; upload endpoint lives in the caller's
  service — inspections use `parse-voice`, advisor uses `/advisor/transcribe`).
- `features/advisor/`: `AdvisorPage` (responsive list + thread), `ChatThread` (bubbles, typing
  indicator "Savjetnik piše…", auto-scroll), `ChatInput` (textarea + mic; recording → transcript lands
  in the textarea for review before sending; a failed send keeps the text).
- Sidebar "AI Savjetnik" (`Bot`); route `/advisor` (all roles); `?beehiveId=` opens a new conversation
  bound to that hive. `BeehiveDetailPage` has a **"Pitaj savjetnika"** button. Disclaimer footer on the
  thread.

## Tests

`AdvisorContextBuilderTests` (full/empty data, 200-char truncation) and `AdvisorServiceTests`
(non-owner → 404, 60-message cap, AI failure persists nothing, successful create order, hive-access
enforced before the AI call, empty transcript → 400) — Groq mocked via `IAdvisorAiClient`.
