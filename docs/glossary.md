# Glossary

Domain terms used in Melarium. Use these names exactly — in code, docs, and UI labels.

---

## Domain Terms

| Term | Code Name | Definition |
|---|---|---|
| Apiary | `Apiary` | A physical location (yard/field) where beehives are kept. Called *pčelinjak* in Bosnian. |
| Beehive | `Beehive` | An individual hive box. Called *košnica* in Bosnian. Belongs to one Apiary. |
| Inspection | `Inspection` | A recorded hive check. Captures health indicators at a point in time. Called *pregled*. |
| Diet | `Diet` | A structured feeding program for one beehive with a defined reason, food type, and schedule. |
| Feeding Entry | `FeedingEntry` | One scheduled feeding event within a Diet. Has a date and completion status. |
| Todo | `Todo` | A task reminder scoped to either an Apiary or a Beehive (never both). |
| Organization | `Organization` | The top-level tenant. Each organization has its own users and apiaries. |
| User | `User` | A person with access to one Organization. Has role Admin or SystemAdmin. |
| Queen | `Queen` | The queen bee of a colony. Called *matica* in Bosnian. A Beehive has at most one Active queen; older records form the replacement history. |
| Harvest | `Harvest` | A honey extraction event (*vrcanje*), apiary-scoped, dated, with one line per hive. |
| Harvest Entry | `HarvestEntry` | The per-hive line of a Harvest: kg extracted (and optional frames) from one Beehive. |
| Yield | `TotalKg` / *prinos* | Honey extracted, in kg — per harvest, per hive/season, or aggregated on the Stats page. |
| Honey Type | `HoneyType` | Botanical honey variety of a harvest. English enum (`Acacia`…), Bosnian labels via `BsLabels` (Bagrem, Lipa, Kesten, Suncokret, Livadski, Šumski, Uljana repica, Ostalo). |
| Assistant Session | `AiAssistantSession` | A personal AI Asistent conversation thread (SPEC-17/18), optionally bound to a hive. Owned by one user; never org-shared. Replaced the retired *AI Savjetnik*'s `AdvisorConversation`. |
| Assistant Turn | `AiAssistantTurn` | One turn in a Session (`Role` = User or Assistant) — a typed/spoken command, a question, or the assistant's reply/answer. May carry proposed Actions (empty for a Q&A turn). |
| Assistant Action | `AiAssistantAction` | One proposed create/update/complete/delete on a Turn — a `Pending` card until the user confirms or rejects it. |
| Supersedure | `QueenOrigin.Supersedure` | *Tiha zamjena* — the colony replaces its queen on its own, without beekeeper intervention or swarming. |
| QR Code | `qrCode` | A Base64 PNG image encoding a Beehive's `uniqueId`. Used for physical hive scanning. |
| Unique ID | `uniqueId` | A Guid assigned to a Beehive at creation. Stable, never changes. Encoded in the QR code. |
| Treatment | `Treatment` | A veterinary medicine application event (*tretman*), apiary-scoped, with one line per treated hive. Part of the legally required medicine register (evidencija tretmana). |
| Treatment Entry | `TreatmentEntry` | The per-hive line of a Treatment; optional `DoseNote` when the hive's dose deviates from `DosePerHive`. |
| Karenca | `WithdrawalDays` / `KarencaUntil` | Withdrawal period — days after a treatment ends during which honey must not be extracted for human consumption. `karencaUntil = (endDate ?? startDate) + withdrawalDays`; many registered bee products have karenca 0. |
| LOT broj | `BatchNumber` | The batch/serial number from the medicine packaging. Legally expected in the treatment register (traceability). |
| Active Substance | `ActiveSubstance` | The medicine's active compound (Amitraz, Oksalna kiselina…). English enum, Bosnian labels via `BsLabels`. |
| Learning Topic | `LearningTopic` | A platform-wide educational article (*Edukacija*), authored by SystemAdmin, markdown body, optionally tied to months (seasonal) — otherwise evergreen. |
| Pasture | `Pasture` | An org-scoped named grazing location (*pašnjak*) for migratory beekeeping. Reusable season after season; may host several apiaries at once. |
| Apiary Move | `ApiaryMove` | One relocation event (*selidba*): apiary → pasture on a date. Updates the apiary's current pasture and snapshots the pasture's coordinates onto the apiary. |
| Svjedodžba | `CertificateNumber` | The veterinary certificate number legally expected when relocating hives; recorded per move. |
| Matična lokacija | `CurrentPastureId == null` | The apiary's original home location, before any recorded move; also the stats bucket for pre-first-move harvests. |
| Sastavljanje društava | `BeehiveMerge` | Uniting two colonies into one (SPEC-19). Called *spajanje* in most literature; Melarium says **sastavljanje** because *spajanje* is already SPEC-18's title. |
| Pripojena košnica | `SourceBeehiveId` | The hive whose colony is merged away. It leaves the apiary permanently but is never deleted — its history, including treatment entries, stays readable. |
| Prijemna košnica | `TargetBeehiveId` | The hive that receives the colony and stays in the apiary. May receive several colonies over the years. |
| Bezmatak | `MergeReason.Queenless` | A colony with no queen — the commonest reason to merge. |
| Lažne matice | `MergeReason.LayingWorkers` | Laying workers; such a colony cannot be saved by adding a queen and will kill an introduced one. |
| Undo journal | `UndoJournalJson` | Snapshot of everything a merge changed outside its own table, so the 24-hour undo can restore it exactly — including the todos it deleted, which no other row remembers. |

---

## Role Terms

| Term | Meaning |
|---|---|
| `Admin` | Standard user. Manages apiaries, beehives, and all child entities within their organization. |
| `SystemAdmin` | Platform administrator. Manages organizations and users. Has access to `/api/admin`. |

---

## Status Terms

| Enum | Values | Domain Meaning |
|---|---|---|
| `DietStatus` | `NotStarted` | Diet created but no entries completed yet. Editable. |
| | `InProgress` | At least one feeding entry completed. No longer editable. |
| | `Completed` | All feeding entries completed. |
| | `StoppedEarly` | Manually terminated via "complete early" action with a comment. |
| `FeedingEntryStatus` | `Pending` | Scheduled but not yet done. |
| | `Completed` | Marked as done by the user. |
| `HoneyLevel` | `Low / Medium / High` | Estimated honey store level observed during an inspection. |
| `NotificationType` | `InspectionOverdue / HoneyLevelDrop / FrostWarning / OldQueen` | Smart-alert notifications raised by the daily `AlertScanWorker` (SPEC-04). |
| | `WeeklySummary` | Monday AI-written weekly digest per organization. |
| `QueenStatus` | `Active` | The hive's current queen (at most one per hive). |
| | `Replaced / Died / Missing` | Historical states; `EndDate` records when the queen stopped being active. |
| | `Removed` | Physically removed by the beekeeper — today only when a colony merge does not keep her (SPEC-19). |
| `MergeReason` | `Queenless / LayingWorkers / WeakColony / PoorQueen / Consolidation / Robbing / Other` | Why two colonies were united. |
| `MergeMethod` | `Newspaper / Direct / Other` | How they were physically united — over a sheet of newspaper (the usual way) or directly with the scents masked. |
| `MergeQueenOutcome` | `KeptTarget / KeptSource / None` | Which queen survives. Always chosen, never assumed: a queenless receiving colony keeps the queen that arrives with the merged-in one. |
| `TodoPriority` | `Low / Medium / High` | Urgency of a task. |

---

## Architecture Terms

| Term | Meaning |
|---|---|
| Clean Architecture | The 4-layer backend pattern: API → Application → Domain → Infrastructure. |
| UoW | Unit of Work. The `IUnitOfWork` interface that coordinates repositories and `SaveChangesAsync()`. |
| Repository | A typed data-access class wrapping EF Core queries for one entity. |
| DTO | Data Transfer Object. Input/output contract between API and client. Never the domain entity itself. |
| Feature Slice | One folder in `Application/` containing service + DTOs + validators for one domain concept. |
| React Query | TanStack Query v5. Used for all server-state fetching, caching, and mutation in the frontend. |
| PWA | Progressive Web App. The frontend is installable and has offline caching via Workbox. |
| Problem Details | RFC 7807 JSON error format returned by the API for all error responses. |

---

## Diet Reason Terms

| Code | Human Label | When Used |
|---|---|---|
| `LackOfFood` | Lack of Food | Colony is starving or stores critically low |
| `WinterFeeding` | Winter Feeding | Pre-winter or winter supplemental feeding |
| `SpringStimulation` | Spring Stimulation | Early spring to trigger brood production |
| `SummerDearth` | Summer Dearth | Feeding during nectar gap in summer |
| `PreWinterPrep` | Pre-Winter Prep | Late summer preparation before winter |
| `ColonyReinforcement` | Colony Reinforcement | Strengthen a weak colony |
| `OrphanColony` | Orphan Colony | Colony lost its queen |
| `AfterTreatment` | After Treatment | Recovery feeding post-medication |
| `Other` | Other | Any other reason (free-text notes apply) |
