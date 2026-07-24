# SPEC-05 — Inspection Photos & AI Frame Analysis ("Fotografije pregleda")

| | |
|---|---|
| **Status** | ✅ Implemented (2026-07-05) — both phases; see notes in Acceptance criteria |
| **Effort** | L (~3+ days — introduces file storage) |
| **Depends on** | nothing (Phase 2 reuses `Groq:ApiKey`) |
| **New secrets** | `Storage__*` (S3-compatible credentials) — prod only |
| **New packages** | `AWSSDK.S3` (backend) — **flagged per house rule, approve before implementing** |

## Goal

**Phase 1:** attach photos to inspections — a visual history of each hive (brood pattern, frames,
anything unusual). **Phase 2:** one tap sends a frame photo to a vision model that returns a
structured assessment (brood pattern, queen cells, visible anomalies) in Bosnian.

Phases ship independently — Phase 1 alone is already valuable. **Do not start Phase 2 until
Phase 1 is merged.**

## Why storage first

QR codes live as Base64 in Postgres — fine for 2 KB PNGs, wrong for MB photos (bloats DB,
backups, DTOs). Render's disk is **ephemeral**, so prod needs object storage.

- Abstraction `IFileStorage` (Application): `SaveAsync(stream, contentType) → storagePath`,
  `OpenReadAsync(path)`, `DeleteAsync(path)`.
- `LocalDiskFileStorage` (dev; root from `Storage:LocalPath`, default `./uploads`, git-ignored).
- `S3FileStorage` (prod; any S3-compatible — recommend **Cloudflare R2**, free 10 GB, no egress
  fees). Config: `Storage:Provider = "Local" | "S3"` + `Storage:S3:{Bucket, Endpoint, AccessKey, SecretKey}`
  (secrets via env vars only).
- Photos are **served through the API** (`GET .../photos/{photoId}/file` streams from storage,
  auth-checked) — keeps the bucket private, no presigned-URL machinery, works identically for
  Local and S3. Cache header `private, max-age=86400`.

Record the storage decision in `decisions.md`.

## Phase 1 — Attachments

### Entity

```
InspectionPhoto : BaseEntity
  InspectionId  int     (FK, cascade delete — on inspection delete also delete blobs, see service)
  StoragePath   string(300)
  ContentType   string(50)      // image/jpeg | image/png | image/webp
  SizeBytes     long
  Caption       string(200)?
  AnalysisJson  string?         // Phase 2; null until analyzed
```

`IInspectionPhotoRepository` + `IUnitOfWork` property. Migration `AddInspectionPhotos`.

### Rules & endpoints

- Max **5 photos per inspection**, max **8 MB each**, content types above only (validate real
  header bytes, not just extension). Server re-encodes? **No** (out of scope) — but strip nothing,
  store as-is; document that EXIF (incl. GPS) is preserved → mention in UI hint.
- Delete inspection → service deletes blobs best-effort (log failures, never block the delete).

| Method | Path | Notes |
|---|---|---|
| POST | `/api/inspections/{id}/photos` | multipart (file + optional caption) → `InspectionPhotoDto` `201` |
| GET | `/api/inspections/{id}/photos` | → `InspectionPhotoDto[]` (no image bytes; frontend builds file URLs) |
| GET | `/api/inspections/photos/{photoId}/file` | streams the image (auth-checked) |
| DELETE | `/api/inspections/photos/{photoId}` | `204`; deletes blob |

Authorization via `IAccessGuard`: same rights as the parent inspection (view → view, edit → add/delete).
Rate limit uploads with the default pipeline (no special policy; size limit is the guard).

### Frontend (Phase 1)

- `InspectionFormPage`: photo picker (`<input type="file" accept="image/*" capture="environment">`),
  thumbnails with remove; uploads happen **after** the inspection is saved (create flow: save →
  upload sequentially → navigate; failed upload → toast with retry, inspection is not rolled back).
- Inspection detail/list on `BeehiveDetailPage`: thumbnail strip → lightbox (simple modal, no new deps).
- Auth note: `<img src>` can't send the Bearer header — fetch blobs via `apiClient` (respects the
  existing refresh-on-401) and use object URLs; revoke on unmount.

## Phase 2 — AI frame analysis

- `POST /api/inspections/photos/{photoId}/analyze` → runs vision model → persists `AnalysisJson` →
  returns DTO. Re-analyze overwrites. Rate limit: new policy `photo-analyze` **5/min per IP**.
- Model: Groq multimodal Llama 4 (e.g. `meta-llama/llama-4-scout-17b-16e-instruct` — **verify the
  current recommended vision model id on Groq at implementation time**; keep the model id in config
  `Groq:VisionModel`). Image sent base64 in the chat payload (Groq OpenAI-compatible format).
- Prompt (Bosnian, JSON-forced like `VoiceParsingService`): input is a beehive frame/comb photo;
  return:

```json
{
  "isFramePhoto": true,          // false → UI says photo doesn't look like a frame; rest null
  "broodPattern": 4,             // 1–5 or null (compactness/coverage)
  "queenCellsVisible": false,
  "anomalies": ["..."],          // short Bosnian phrases, [] if none
  "summary": "..."               // 2–3 sentences, Bosnian
}
```

- Guardrail in prompt + UI footer: *"Analiza je informativna i ne zamjenjuje pregled stručnjaka."*
  Never claim disease diagnosis — anomalies phrased as observations ("moguće…").
- UI: "Analiziraj (AI)" button on the lightbox; result panel with pattern stars, chips, summary.

## Out of scope

Photos on apiary/hive profiles; offline photo queue (see SPEC-07 note); automatic analysis on
upload; image resizing/thumbnails server-side (client shows full image scaled); EXIF stripping;
varroa **counting** (board photos) — possible future spec.

## Acceptance criteria

- [x] Phase 1: upload works (live-verified in dev: browser file → 201 → strip → lightbox; camera
      input uses `capture="environment"` + separate gallery picker — **real-phone camera check still
      owed**); 6th photo, 9 MB file and fake-header file rejected with Bosnian errors (unit tests;
      fake JPEG live-verified → 422).
- [x] Photos visible only to users who can view the inspection (guard unit tests); file URL is
      `[Authorize]`d (unauthenticated → 401 via JWT middleware).
- [x] Deleting inspection removes DB rows (FK cascade) and blobs best-effort (unit tests incl.
      partial-failure continuation).
- [x] Works with `Storage:Provider=Local` (dev, live-verified) and `=S3` with no code changes —
      config-only switch; **S3/R2 staging smoke test still owed** (needs bucket credentials).
- [x] Phase 2: non-frame photo → `isFramePhoto=false` empties the assessment (parser unit test) and
      the UI renders the "nije okvir" panel. **Live Groq call still owed — dev `Groq:ApiKey` was
      empty at implementation time** (endpoint verified up to Groq's 401).
- [x] Phase 2: analysis JSON parses robustly (malformed output → Bosnian 422, photo unaffected —
      unit tests). Note: Groq caps base64 requests at 4 MB → photos over ~3 MB raw get a Bosnian
      422 before the Groq call (resizing out of scope, see ADR-027).
- [x] Docs updated: `features/inspection-photos.md`, `api-contracts.md`, `context.md`
      (new config + package + storage decision in `decisions.md` ADR-027), this spec → ✅.
