# Feature: Apiaries

## Overview

An apiary is a physical location where beehives are kept. It belongs to an Organization and holds geographic coordinates for weather lookups. It is the top-level container for beehives and apiary-scoped todos.

## Domain Rules

- An apiary belongs to exactly one Organization
- `Latitude` and `Longitude` are optional but required for weather feature
- `Location` is a human-readable string (e.g. "North Field", "Back Garden")
- Deleting an apiary cascades to its beehives (and transitively to inspections, diets, entries, todos)

## API

- `GET /apiaries` — all apiaries for the authenticated user's organization
- `GET /apiaries/{id}` — detail including `beehives[]`
- `POST /apiaries` — create
- `PUT /apiaries/{id}` — update
- `DELETE /apiaries/{id}` — cascade delete
- `GET /apiaries/{id}/weather` — 7-day forecast from Open-Meteo using stored coordinates

## Weather Sub-Feature

- Fetches from `api.open-meteo.com` using apiary `latitude` + `longitude`
- Returns 7-day daily forecast: min/max temperature, precipitation, wind speed
- Frontend caches weather response for 30 minutes (React Query stale time)
- If coordinates are missing, return a `BusinessRuleException` (422) explaining the requirement

## UI Rules

- `ApiaryListPage` shows all apiaries as cards with location + beehive count
- `ApiaryDetailPage` shows apiary info, weather widget, beehive list, and todo section
- Weather widget: horizontal scroll of 7 day cards (icon, date, temp range, rain)
- "Add Beehive" button on detail page pre-populates `apiaryId`

## Edge Cases

- Organization with no apiaries shows an empty state with a CTA to create one
- Weather endpoint gracefully handles Open-Meteo being unavailable (surface error to UI, don't crash)
- Cascade delete is destructive — UI should show a confirmation dialog before calling DELETE

## Validation Rules

```
name:       required, max 100 chars
location:   optional, max 200 chars
latitude:   optional, between -90 and 90
longitude:  optional, between -180 and 180
```
