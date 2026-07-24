# Feature: Diets (Feeding Programs)

## Overview

A diet is a structured feeding program for a beehive. It defines what food is given, why, how often, and for how long. Individual scheduled feeding events are tracked as **FeedingEntries**.

## Domain Rules

- A diet belongs to exactly one beehive
- `Status` transitions: `NotStarted` → `InProgress` → `Completed` | `StoppedEarly`
- Status is NOT manually set — it is computed or changed via dedicated actions:
  - Starting/completing entries changes status to `InProgress`
  - `POST /diets/{id}/complete-early` sets status to `StoppedEarly`
  - When all entries are completed, status becomes `Completed`
- `FeedingEntries` are generated automatically when a diet is created (based on `duration` and `frequency`)
- Each `FeedingEntry` has a `ScheduledDate` and `Status` (Pending / Completed)

## Enums

```
DietStatus:  NotStarted | InProgress | Completed | StoppedEarly
DietReason:  LackOfFood | WinterFeeding | SpringStimulation | SummerDearth |
             PreWinterPrep | ColonyReinforcement | OrphanColony | AfterTreatment | Other
FoodType:    SugarSyrup | Fondant | Pollen | ProteinPatties | Custom
```

## API

- `GET /diets/by-beehive/{beehiveId}` — all diets for a beehive
- `GET /diets/{id}` — detail including `feedingEntries[]`
- `POST /diets` — create diet and auto-generate feeding entries
- `PUT /diets/{id}` — update metadata (only allowed if `NotStarted`)
- `DELETE /diets/{id}` — delete diet and all entries
- `POST /diets/{id}/complete-early` — body: `{ comment }`, marks diet as `StoppedEarly`
- `POST /diets/{id}/entries/{entryId}/complete` — marks one entry as Completed

## UI Rules

- `DietSection` component embedded on `BeehiveDetailPage` shows all diets with status badge
- Clicking a diet navigates to `DietDetailPage` showing the diet info + feeding entry checklist
- Completed entries shown with strikethrough; pending entries have a "Mark Complete" button
- "Complete Early" button only visible when status is `InProgress`
- "Edit" button only visible when status is `NotStarted`

## Edge Cases

- Editing a diet in `InProgress` or `Completed` state should return a `BusinessRuleException` (422)
- Completing an already-completed entry should be a no-op or return 422
- If a beehive is deleted, all diets and their feeding entries are cascade-deleted
- `FoodType.Custom` allows a free-text description field

## Validation Rules

```
beehiveId:   required
startDate:   required
reason:      required, valid DietReason enum
duration:    required, > 0 (days)
frequency:   required, > 0 (days between feedings)
foodType:    required, valid FoodType enum
```
