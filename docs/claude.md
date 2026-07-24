# Claude — Project Rules

> Read this file first. It overrides generic Claude defaults for this project.

## Project Identity

Melarium is a beekeeping management app. Users manage **apiaries → beehives → inspections/diets/todos**.
Backend: .NET 10 Clean Architecture. Frontend: React 18 + TypeScript + Vite + TanStack Query.

## Non-Negotiables

- **Never** mix layers: controllers call services, services call repositories, repositories call EF Core.
- **Never** expose domain entities from API endpoints — always use DTOs.
- **Never** put business logic in controllers or repositories.
- **Never** bypass `IUnitOfWork.SaveChangesAsync()` — do not call `_context.SaveChangesAsync()` directly.
- **Never** write raw SQL — use EF Core LINQ only.
- **Never** commit secrets anywhere — the repo is public. `appsettings*.json` keeps empty placeholders;
  real values live in env vars (production) or user-secrets / `appsettings.Development.json` (local dev only).

## Code Generation Rules

- Match existing naming conventions exactly (see `coding-guidelines.md`).
- New features follow the feature-slice pattern (see `common-patterns.md`).
- All new API endpoints must have FluentValidation, not inline validation.
- All new React Query hooks go in `queries.ts` or `adminQueries.ts`.
- Do not add packages without asking — check if existing deps cover the need.
- Do not add comments explaining *what* code does — only *why* when non-obvious.
- Do not create files outside the established folder structure without justification.

## Decision Hierarchy

When in doubt: check `decisions.md` → `architecture.md` → `common-patterns.md`.

## Scope Discipline

- Bug fix = fix only the bug. No surrounding cleanup.
- Feature = implement only what was asked. No speculative additions.
- Refactor = structural change only. No behavior changes.

## Before Every Task

1. Check `ignore.md` — confirm you are not touching frozen code.
2. Check `context.md` — confirm the feature doesn't already exist.
3. Follow the steps in `workflow.md`.

## Files to Read for Feature Work

| Task | Read first |
|---|---|
| Any task | `ignore.md`, `context.md` |
| New backend feature | `architecture.md`, `common-patterns.md`, `workflow.md` |
| New API endpoint | `api-contracts.md` |
| New frontend page | `common-patterns.md`, `coding-guidelines.md` |
| Specific feature change | `docs/features/<feature>.md` |
| Unfamiliar domain term | `glossary.md` |
