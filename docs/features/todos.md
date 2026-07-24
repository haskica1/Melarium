# Feature: Todos

## Overview

Todos are task reminders that can be scoped to either an **Apiary** or a **Beehive** — but not both simultaneously. They track actionable items with priority and due dates.

## Domain Rules

- A todo must belong to either `apiaryId` OR `beehiveId` — exactly one, never both, never neither
- `Priority` is Low / Medium / High
- `IsCompleted` is a boolean toggled via a standard update (`PUT /todos/{id}`)
- No automatic status transitions — completion is purely manual

## API

- `GET /todos/by-apiary/{apiaryId}` — todos scoped to an apiary
- `GET /todos/by-beehive/{beehiveId}` — todos scoped to a beehive
- `GET /todos/{id}` — single todo
- `POST /todos` — create (requires exactly one of `apiaryId` / `beehiveId`)
- `PUT /todos/{id}` — update any field including `isCompleted`
- `DELETE /todos/{id}` — hard delete

## UI Rules

- `TodoSection` component reused on both `ApiaryDetailPage` and `BeehiveDetailPage`
- Component receives either `apiaryId` or `beehiveId` as prop — determines which endpoint to call
- Todos sorted by: incomplete first, then by `dueDate` ascending, then by `priority` descending
- Overdue todos (past `dueDate`, not completed) shown with a red indicator
- "Add Todo" form pre-populates the correct scope (apiary or beehive) based on context

## Edge Cases

- Deleting an apiary cascades to all apiary-scoped todos
- Deleting a beehive cascades to all beehive-scoped todos
- A todo with no `dueDate` is valid; treated as lowest urgency in sort
- Completed todos remain visible (no archive/hide by default)

## Validation Rules

```
title:      required, max 200 chars
priority:   required, valid TodoPriority enum
dueDate:    optional, must be valid date if provided
apiaryId:   required if beehiveId is null
beehiveId:  required if apiaryId is null
isCompleted: defaults to false on create
```

## Business Rule (Enforced in Service)

```csharp
if (dto.ApiaryId is null && dto.BeehiveId is null)
    throw new BusinessRuleException("Todo must belong to an apiary or beehive.");
if (dto.ApiaryId is not null && dto.BeehiveId is not null)
    throw new BusinessRuleException("Todo cannot belong to both an apiary and a beehive.");
```
