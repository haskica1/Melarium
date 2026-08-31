# Inspection Photos & AI Frame Analysis (SPEC-05)

Photos attached to inspections (visual hive history) + optional one-tap AI assessment of a
frame photo in Bosnian. Implemented 2026-07-05.

## Storage

- `IFileStorage` abstraction (`Application/Common/Interfaces`): `SaveAsync(stream, contentType) → storagePath`,
  `OpenReadAsync`, `DeleteAsync` (idempotent). One implementation, in `Melarium.Infrastructure/Storage/`:
  - **`LocalDiskFileStorage`** — root `Storage:LocalPath`: `./uploads` (git-ignored) in dev,
    `/app/uploads` in prod, which docker-compose mounts as the persistent `uploads-data` volume.
- Keys are date-partitioned GUIDs (`2026/07/<guid>.jpg`).
- **Photos are served through the API** (`GET /inspections/photos/{id}/file`, auth-checked,
  `Cache-Control: private, max-age=86400`) — storage paths are internal and never exposed as public
  URLs, so the storage backend can be swapped without touching callers. Rationale in `decisions.md`.
- ADR-027 originally paired this with an `S3FileStorage` for production, because Render's disk was
  ephemeral. That implementation (and the `AWSSDK.S3` package) was **removed on 2026-08-31** with the
  move to the netcup VPS, where the volume is persistent. `git log -- backend/Melarium.Infrastructure/Storage/`
  has it if object storage is ever needed again.

## Rules

- Max **5 photos per inspection**, max **8 MB**, `image/jpeg|png|webp` — content type is detected
  from **real header bytes** (client-supplied type/extension untrusted). Violations → `422` with a
  Bosnian message (`BusinessRuleException`).
- EXIF (incl. GPS) is **preserved** — the upload form shows a hint.
- Authorization mirrors the parent inspection via `IAccessGuard.EnsureCanAccessBeehiveAsync`.
- Deleting a photo/inspection removes blobs **best-effort** (log-and-continue, never blocks the delete);
  a failed DB insert after a blob write cleans the blob up.

## AI analysis (Phase 2)

- `POST /inspections/photos/{photoId}/analyze` → `GroqPhotoAnalysisAiClient` (`Features/Ai`) —
  Groq vision model from config **`Groq:VisionModel`** (via `GroqModels.Vision`; default
  `qwen/qwen3.6-27b` since 2026-08-17 — `meta-llama/llama-4-scout-17b-16e-instruct` was retired by
  Groq on 2026-07-17, see ADR-035), image base64 in the OpenAI-compatible payload, JSON output
  forced, temperature 0.
  **A replacement must do vision *and* `json_object` in the same request** — this caller forces JSON
  while sending an image, and a vision model without JSON mode fails in a way that reads as an outage.
- Result persisted to `InspectionPhoto.AnalysisJson` (camelCase, relaxed escaping for č/ć/š);
  re-analyze overwrites. Parsed shape: `{ isFramePhoto, broodPattern 1–5|null, queenCellsVisible?,
  anomalies[], summary? }` — out-of-range scores dropped, non-frame photos empty the assessment.
- **Groq caps base64 requests at 4 MB** → images over ~3 MB raw are rejected with a Bosnian `422`
  before calling Groq (server-side resizing was out of SPEC-05 scope — future improvement).
- Malformed model output → `422` *"AI analiza nije uspjela…"*, photo untouched.
- Rate limit: policy `photo-analyze`, **5/min per IP**. Guardrail in prompt + UI footer:
  *"Analiza je informativna i ne zamjenjuje pregled stručnjaka."* — anomalies are phrased as
  observations ("moguće…"), never diagnoses.

## Frontend

- `core/services/inspectionPhotoService.ts` — CRUD + `analyze` + `fetchImageBlob` (authenticated
  blob fetch; `<img src>` can't send the Bearer header) + client-side `validatePhotoFile` mirror.
- `features/inspections/InspectionPhotos.tsx` — `AuthImage` (object URLs, revoked on unmount),
  `InspectionPhotoStrip` (renders nothing when no photos; mounted on every BeehiveDetailPage
  timeline item), `PhotoLightbox` (prev/next, Escape, delete, "Analiziraj (AI)" + result panel:
  pattern stars, chips, summary, guardrail footer).
- `InspectionFormPage` — "Uslikaj" (`capture="environment"`) + "Iz galerije" pickers, pending
  previews with remove, **uploads run after the inspection is saved** (sequential); failed uploads
  keep the saved inspection, stay on the page and the submit button becomes "Pošalji fotografije"
  (retry-only). Offline (SPEC-07 outbox) saves the inspection JSON only — photos show a toast and
  must be added later (offline photo queue out of scope).
- Hooks in `queries.ts`: `useInspectionPhotos`, `useDeleteInspectionPhoto`, `useAnalyzeInspectionPhoto`
  (key `inspectionPhotos(inspectionId)`).

## Tests

`InspectionPhotoServiceTests` (upload rules, sniffing, cleanup semantics, guard, analyze persist/fail),
`InspectionServiceDeleteTests` (blob cleanup best-effort), `PhotoAnalysisParsingTests` (robust JSON
parse). Live-verified E2E in dev (upload → strip → lightbox → 422 for fake JPEG); the actual Groq
vision call needs a valid `Groq:ApiKey` (empty in local dev at implementation time).
