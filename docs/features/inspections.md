# Feature: Inspections

## Overview

An inspection records the state of a beehive at a specific point in time. Each inspection is tied to one beehive and captures health indicators observed during a physical hive check.

## Domain Rules

- An inspection belongs to exactly one beehive
- `Date` is required and must not be in the future
- `Temperature` is in Celsius, range: -30°C to 60°C
- `HoneyLevel` is an enum — Low / Medium / High
- `BroodStatus` is a free-text field (describes queen activity, brood pattern, etc.)
- `Notes` is optional free text
- Inspections are returned newest-first by default

## API

- `GET /inspections/by-beehive/{beehiveId}` — all inspections for a beehive, ordered by date desc
- `GET /inspections/{id}` — single inspection
- `POST /inspections` — create (requires `beehiveId`, `date`, `honeyLevel`)
- `PUT /inspections/{id}` — update any field
- `DELETE /inspections/{id}` — hard delete

## UI Rules

- Inspections displayed as a chronological list on `BeehiveDetailPage`
- Each inspection entry shows: date, honey level badge, temperature, short brood status
- Clicking an entry navigates to `InspectionFormPage` in edit mode
- "Add Inspection" button on beehive detail pre-populates `beehiveId` via query param or route state

## Edge Cases

- Multiple inspections on the same date are allowed
- Updating an inspection does not re-trigger any automated action
- Deleting a beehive cascades to all its inspections

## Validation Rules

```
date:         required, not future
temperature:  optional, between -30 and 60
honeyLevel:   required, valid enum value
broodStatus:  optional, max 500 chars
notes:        optional, max 1000 chars
```
