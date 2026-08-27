# Feature: Colony Merge (Sastavljanje društava)

## Overview

Two colonies united into one. The **source** hive (*pripojena*) leaves the apiary permanently; the
**target** hive (*prijemna*) stays and carries a record of the colony it received. Implemented per
[SPEC-19](../specs/SPEC-19-colony-merge.md).

Nothing is deleted. The source hive's row and all its history — inspections, harvests and above all
its **legally retained treatment entries** (5-year duty) — stay readable by id. Only the *lists*
stop showing it. That is the reason this feature exists at all: `DELETE /api/beehives/{id}` cascades
into `TreatmentEntry`, so removing a hive by deletion destroys a legal record.

## Domain Rules

- The merge is recorded from the hive that **disappears**. Practice unites the weaker colony into the
  stronger one, which keeps its own stand.
- `Beehive.MergedIntoBeehiveId` (self-FK) is the state: non-null = out of the apiary. `BeehiveMerge`
  is the event (reason, method, queen, author, undo journal). Same split as SPEC-10's
  `Apiary.CurrentPastureId` + `ApiaryMove`.
- A receiving hive may collect **several** merges over the years; a source hive can have at most one
  in force (unique index on `SourceBeehiveId` filtered on `UndoneAt IS NULL`).
- **Cross-apiary merges are allowed.** The caller must be able to manage *both* apiaries, and both
  must belong to the same organization.
- **Which queen survives is chosen, never assumed** — practice removes the weaker colony's queen, but
  when the receiving colony is queenless the surviving queen is the one that comes with the merged-in
  colony. `KeptSource` closes the target's queen **first**, then moves the source's queen over; the
  reverse order would briefly leave two active queens in one hive, which `QueenService.UpdateAsync`
  refuses outright.
- A merged-away hive **stops counting toward the plan limit** (`MaxBeehives`) — the colony genuinely
  no longer exists.
- **The archive is permanent.** A merged hive never returns to service; when the emptied box is
  repopulated the beekeeper creates a *new* hive with a new QR code, so the histories of two different
  colonies never mix in one record.
- **Karenca is a warning, not a block.** If the source hive is inside a withdrawal period, its bees
  carry it into the receiving hive; the confirm dialog says so and lets the beekeeper decide.

## What a merge changes

| Thing | What happens |
|---|---|
| Source hive | `MergedIntoBeehiveId` + `MergedAt` set; disappears from every list |
| Queens | Per `MergeQueenOutcome`; the closed queen(s) get `QueenStatus.Removed`, `EndDate`, and a note |
| Open hive todos | Deleted (completed ones and apiary-level ones are untouched) |
| Feeding | The hive is taken **off the programme** (`DietBeehive.RemovedOn`). The programme only stops early — with a comment — when this was its **last** active hive |
| Treatments in progress | `TreatmentEntry.DoseNote` gets *"Prekinuto … — društvo sastavljeno s košnicom X"*. The row is **never deleted** and the PDF register is unchanged (`DoseNote` is not printed) |
| Inspections, harvests, photos, assigned beekeepers, QR code | Untouched — they stay on the archived hive |
| Notification | `NotificationType.BeehiveMerged` to the actor's superior, same audience as a new hive |

All of it lands in **one `SaveChangesAsync()`**. A half-applied merge — a hive flagged as merged whose
queen is still active — is the one state the undo journal cannot repair.

## Undo (24 hours)

`POST /api/beehive-merges/{id}/undo` reverses everything, but only within 24 hours **of the merge
being recorded** (`CreatedAt`, not `MergedAt`, which may be backdated). The deadline is computed
server-side (`MergeUndoPolicy`) and shipped to the client as `canUndoUntil`; the client never derives
it.

Deleted todos cannot be reconstructed from the database, so the merge writes
`BeehiveMerge.UndoJournalJson` — a snapshot of everything it changed outside its own table (queens'
prior status/hive/notes, the todos by value, the `DietBeehive` row ids, the previous `DoseNote`s).
Restoring runs in reverse order and clears `RemovedOn` on the **same** `DietBeehive` row: a fresh row
would carry a new `CreatedAt`, which is "when the hive joined the programme" and feeds the
consumption maths. Restored todos get new ids — nothing points at a todo id, so that is the one thing
undo does not reproduce exactly.

An undone merge keeps its row with `UndoneAt` set: it stops being shown, it does not vanish.

## Where merged hives are filtered out

Thirteen read sites, listed in SPEC-19 §5. The three that carry the rest:

- `BeehiveRepository.GetByApiaryIdAsync` / `GetByOrganizationAsync` / `CountByOrganizationAsync`
- `AccessGuard.GetAccessibleBeehivesAsync` — the single source both the hive list and the AI assistant
  read, so those two can never drift apart
- `ApiaryRepository.GetWithBeehivesAsync` (filtered `Include`) — the hive list on the apiary page

**Not** filtered, deliberately: `GetByIdAsync`, `GetWithInspectionsAsync` and `GetByUniqueIdAsync`. A
merged hive must stay reachable by direct link, from the archive, from the treatment register, and by
scanning the sticker still stuck on the emptied box.

> An EF **global query filter** would have done all thirteen in one line and was rejected:
> `TreatmentEntry.Beehive`, `HarvestEntry.Beehive` and `Inspection.Beehive` are *required* navigations,
> so EF would silently return them as `null` and the treatment register would print without hive
> names — exactly the record this feature refuses to damage. See ADR-038.

## Enums

```
MergeReason        Queenless | LayingWorkers | WeakColony | PoorQueen | Consolidation | Robbing | Other
MergeMethod        Newspaper | Direct | Other
MergeQueenOutcome  KeptTarget | KeptSource | None
QueenStatus       += Removed   (queen physically removed; today only via a merge)
```

English enum names, Bosnian labels via `BsLabels` — same convention as Treatments and Diets.

## API (`/api/beehive-merges`)

- `POST /beehive-merges` — `{ sourceBeehiveId, targetBeehiveId, mergedAt, reason, method,
  queenOutcome, notes? }` → 201 `BeehiveMergeDto`
- `POST /beehive-merges/{id}/undo` → 200 `BeehiveMergeDto`
- `GET /beehive-merges/by-beehive/{beehiveId}` — merges this hive **received**, in force only
- `GET /beehive-merges/preview?sourceBeehiveId=&targetBeehiveId=` — consequence counts for the dialog
- `GET /beehives/merged?apiaryId=` — the archive; the only list endpoint that returns merged hives

`BeehiveDto` carries `mergedIntoBeehiveId` / `mergedIntoBeehiveName` / `mergedAt`;
`BeehiveDetailDto` adds `mergeId` + `canUndoUntil`; `BeehiveScanDto` carries the merge target so an
old QR code resolves to a message instead of a 404.

## Access

Same matrix as beehive management. Merging requires `EnsureCanManageApiaryAsync` on **both**
apiaries. A Beekeeper sees the archive filtered to their assigned hives — being merged away never
widens what anyone can see.

## UI

- **`BeehiveDetailPage`** — "Sastavi društvo" button (managers, non-merged hives only). A merged hive
  shows a banner naming the receiving hive and hides every write action; its history reads normally.
- **`MergeColonyModal`** — receiving-hive picker grouped by apiary, date, reason, method, the three
  queen options (none preselected), notes, then a **second confirmation** listing the real
  consequences from `/preview` plus the karenca warning. Same double-confirm shape as destructive AI
  actions.
- **`MergeSection`** — "Sastavljena društva" card on the receiving hive; renders nothing when empty.
  Carries the "Poništi" button while `canUndoUntil` is open.
- **`MergedHivesSection`** — collapsed "Sastavljene košnice" section on `ApiaryDetailPage`; absent
  when the apiary has none. The only path to the archive.
- **`ScanPage`** — scanning a merged hive's code explains where the colony went and offers both hives.

## AI Assistant (SPEC-17/19)

`AiActionKind.MergeBeehive`. `IsDestructive` is **true** — it takes a hive out of the apiary for good,
further than any delete the assistant offers, and the 24-hour undo is a safety net for a misclick, not
a reason to ask less.

The action carries two hives: the source resolves as the normal target, the receiving hive rides in
`AiActionFields.TargetBeehiveId`. The resolver refuses three things outright rather than guessing —
`"sve košnice"`, several source hives, and a merge that never said which queen survives (the card
comes back with the question). Reason and method may default; the queen may not.

The executor calls `IBeehiveMergeService.MergeAsync` — the same service the dialog posts to — and
runs `CreateBeehiveMergeValidator` itself, per SPEC-17 §5.1/§5.2.

## Edge Cases

- Merging a hive with no queen is normal (bezmatak is the commonest reason) and must not error.
- `KeptSource` on a queenless source hive is refused with a Bosnian message.
- A hive that is already merged can be neither source nor target.
- A future `mergedAt` is refused (+1 day tolerance, same as treatments).
- Undo of a merge whose journal is missing (a row written by older code) is refused rather than
  half-applied.

## Implementation Notes

- `BeehiveMergeService` writes through `_uow` rather than calling `DietService`/`TodoService`. Those
  commit internally, which would break the single-transaction rule; nothing is lost by going direct
  since no guard, plan limit or notification cascade hides behind them, and the service performs the
  stricter two-apiary access check itself. The semantics are copied exactly, including
  `RemovedOn = today`.
- The notification is sent **after** the commit (same as `BeehiveService.CreateAsync`) and its failure
  is logged, never thrown — the merge already happened.
- Migration: `AddBeehiveMerge` (two nullable columns on `Beehives`, one new table). Fully additive.
