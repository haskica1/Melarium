# Development Workflow

> Follow this order on every task. Skipping steps causes drift between code and docs.

## Order of Operations

### 1. Understand the Feature
- Read the request carefully. Identify: what entity is affected, what layer is touched, what new behavior is introduced.
- If ambiguous, ask one clarifying question before touching code.

### 2. Check Existing State
- Read `context.md` — confirm the feature doesn't already exist.
- Read `ignore.md` — confirm you are not touching frozen code.
- Read the relevant `features/<name>.md` if the feature already exists.
- **If the task implements a planned feature, read its spec in `specs/` first** — the spec is the
  source of truth for scope, contracts, and acceptance criteria (see `specs/README.md`).

### 3. Check Architecture
- Read `architecture.md` — confirm which layers need to change.
- Read `common-patterns.md` — use the established template; do not invent new structure.
- Read `api-contracts.md` — if adding/changing an endpoint, verify the contract first.

### 4. Implement — Minimal Solution
- Implement only what was asked. No speculative additions.
- Follow the templates in `common-patterns.md` exactly.
- New backend feature checklist:
  - [ ] Domain entity (if new)
  - [ ] Repository interface + implementation
  - [ ] UnitOfWork property added
  - [ ] DTOs + FluentValidation validator
  - [ ] Service interface + implementation
  - [ ] AutoMapper mapping in `MappingProfile.cs`
  - [ ] Controller (thin — delegate to service)
  - [ ] EF Core migration (if schema changed)
  - [ ] Registered in `Program.cs`
- New frontend feature checklist:
  - [ ] Interface in `core/models/index.ts`
  - [ ] Service functions in `core/services/<feature>Service.ts`
  - [ ] React Query hooks in `queries.ts`
  - [ ] Page component in `features/<domain>/`
  - [ ] Route added in `App.tsx`

### 5. Update Documentation
- **New feature**: create `docs/features/<feature>.md`
- **Changed feature**: update existing feature file
- **New endpoint**: update `api-contracts.md`
- **New architectural pattern**: update `common-patterns.md`
- **Architecture decision made**: append to `decisions.md`
- **New term used**: add to `glossary.md`
- **Feature now live**: update `context.md`
- **Frozen code added**: update `ignore.md`

### 6. Verify Before Finishing
- Backend builds without errors
- No new warnings introduced
- Frontend TypeScript compiles clean (no `any`, no type errors)
- No files modified outside the expected scope

---

## When NOT to Refactor

Do not refactor unless explicitly asked. These situations do NOT justify refactoring:
- You notice a naming inconsistency unrelated to your task
- You see a pattern that could be "cleaner"
- A file is long but fully functional

**Only refactor when:** the current structure prevents implementing the requested feature correctly.

## When to Create a New Feature File vs. Update an Existing One

| Situation | Action |
|---|---|
| Completely new domain concept | New `features/<name>.md` |
| Adding behavior to existing entity | Update existing feature file |
| Cross-cutting change (auth, error handling) | Update `architecture.md` or `coding-guidelines.md` |
| New business rule on existing entity | Update feature file + add to `decisions.md` if it was a choice |

## Migration Discipline

- Every schema change requires a new EF Core migration.
- Never edit past migration files.
- Migration name format: `Add<EntityOrField>` or `Update<EntityOrField>` (PascalCase, descriptive).
- Run `dotnet ef database update` after adding a migration.
- Migrations go in `Melarium.Infrastructure/Migrations/`.

## Commit Readiness Checklist

- [ ] Code compiles and runs
- [ ] Feature docs updated
- [ ] `context.md` reflects current state
- [ ] No unrelated files changed
- [ ] `.gitignore` respected (no bin/obj/node_modules staged)
