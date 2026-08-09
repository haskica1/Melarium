// ── User / Auth ───────────────────────────────────────────────────────────────

export enum UserRole {
  ApiaryAdmin       = 'ApiaryAdmin',
  SystemAdmin       = 'SystemAdmin',
  OrganizationAdmin = 'OrganizationAdmin',
  Beekeeper         = 'Beekeeper',
}

// ── Enums ─────────────────────────────────────────────────────────────────────

export enum BeehiveType {
  Langstroth   = 1,
  DadantBlatt  = 2,
  Warré        = 3,
  TopBar       = 4,
  Other        = 5,
}

export const BeehiveTypeLabels: Record<BeehiveType, string> = {
  [BeehiveType.Langstroth]:  'LR (Langstroth-Rutova) košnica',
  [BeehiveType.DadantBlatt]: 'DB (Dadan-Blatt) košnica',
  [BeehiveType.Warré]:       'AŽ (Alberti-Žnideršič) košnica',
  [BeehiveType.TopBar]:      'Pološka košnica',
  [BeehiveType.Other]:       'Ostalo',
}

export enum BeehiveMaterial {
  Wood        = 1,
  Plastic     = 2,
  Polystyrene = 3,
}

export const BeehiveMaterialLabels: Record<BeehiveMaterial, string> = {
  [BeehiveMaterial.Wood]:        'Drvo',
  [BeehiveMaterial.Plastic]:     'Plastika',
  [BeehiveMaterial.Polystyrene]: 'Stiropor',
}

export enum HoneyLevel {
  Low    = 1,
  Medium = 2,
  High   = 3,
}

export const HoneyLevelLabels: Record<HoneyLevel, string> = {
  [HoneyLevel.Low]:    'Nisko',
  [HoneyLevel.Medium]: 'Srednje',
  [HoneyLevel.High]:   'Visoko',
}

// ── Apiary ────────────────────────────────────────────────────────────────────

export interface Apiary {
  id: number
  name: string
  description?: string
  latitude?: number
  longitude?: number
  hasLocation: boolean
  homeLatitude?: number
  homeLongitude?: number
  hasHomeLocation: boolean
  beehiveCount: number
  createdByName?: string
  createdAt: string
}

export interface ApiaryDetail extends Apiary {
  beehives: Beehive[]
}

export interface CreateApiaryPayload {
  name: string
  description?: string
  latitude?: number | null
  longitude?: number | null
}

export interface UpdateApiaryPayload extends CreateApiaryPayload {}

// ── Weather ───────────────────────────────────────────────────────────────────

export interface WeatherForecast {
  latitude: number
  longitude: number
  timezone: string
  currentTemperature?: number
  currentApparentTemperature?: number
  currentWeatherCode?: number
  currentWindSpeed?: number
  currentHumidity?: number
  daily: DailyWeather[]
}

export interface DailyWeather {
  date: string
  maxTemp?: number
  minTemp?: number
  apparentTempMax?: number
  apparentTempMin?: number
  weatherCode?: number
  precipitationSum?: number
  maxWindSpeed?: number
  precipitationProbability?: number
  sunrise?: string
  sunset?: string
  uvIndexMax?: number
}

// ── Beehive ───────────────────────────────────────────────────────────────────

export interface Beehive {
  id: number
  name: string
  type: BeehiveType
  typeName: string
  material: BeehiveMaterial
  materialName: string
  dateCreated: string
  notes?: string
  /** Physical number/label painted on the hive (e.g. "1", "A3"). Used by scan-by-number. */
  labelNumber?: string
  apiaryId: number
  inspectionCount: number
  createdByName?: string
  createdAt: string
  uniqueId?: string
}

export interface BeehiveDetail extends Beehive {
  qrCodeBase64?: string
  inspections: Inspection[]
}

/** QR payload for label printing — fetched on demand via /beehives/by-apiary/{id}/qr-codes. */
export interface BeehiveQr {
  id: number
  name: string
  uniqueId?: string
  qrCodeBase64?: string
}

export interface CreateBeehivePayload {
  name: string
  type: BeehiveType
  material: BeehiveMaterial
  dateCreated: string
  notes?: string
  labelNumber?: string
  apiaryId: number
}

export interface UpdateBeehivePayload extends CreateBeehivePayload {}

/** A hive that matched a scanned/typed number (scan-by-number), with apiary for disambiguation. */
export interface BeehiveMatch {
  id: number
  name: string
  labelNumber?: string
  apiaryId: number
  apiaryName?: string
}

/** Result of resolving a number to hives: `recognizedNumber` null → OCR couldn't read a number. */
export interface BeehiveNumberMatchResult {
  recognizedNumber?: string | null
  matches: BeehiveMatch[]
}

// ── Inspection ────────────────────────────────────────────────────────────────

export interface Inspection {
  id: number
  date: string
  temperature?: number
  honeyLevel: HoneyLevel
  honeyLevelName: string
  broodStatus?: string
  notes?: string
  beehiveId: number
  createdAt: string
}

export interface CreateInspectionPayload {
  date: string
  temperature?: number
  honeyLevel: HoneyLevel
  broodStatus?: string
  notes?: string
  beehiveId: number
}

export interface UpdateInspectionPayload extends CreateInspectionPayload {}

export interface ParseVoiceResult {
  transcript?: string | null
  date?: string | null
  temperature?: number | null
  honeyLevel?: HoneyLevel | null
  broodStatus?: string | null
  notes?: string | null
}

// ── Inspection Photos (SPEC-05) ───────────────────────────────────────────────

export interface InspectionPhoto {
  id: number
  inspectionId: number
  contentType: string
  sizeBytes: number
  caption?: string | null
  /** Raw AI frame-analysis JSON; null until the photo is analyzed (Phase 2). */
  analysisJson?: string | null
  createdAt: string
}

/** Parsed shape of InspectionPhoto.analysisJson (SPEC-05 Phase 2). */
export interface PhotoAnalysis {
  isFramePhoto: boolean
  broodPattern?: number | null
  queenCellsVisible?: boolean | null
  anomalies: string[]
  summary?: string | null
}

// ── Queen ─────────────────────────────────────────────────────────────────────

export enum QueenMarkColor {
  White  = 1,
  Yellow = 2,
  Red    = 3,
  Green  = 4,
  Blue   = 5,
}

export const QueenMarkColorLabels: Record<QueenMarkColor, string> = {
  [QueenMarkColor.White]:  'Bijela',
  [QueenMarkColor.Yellow]: 'Žuta',
  [QueenMarkColor.Red]:    'Crvena',
  [QueenMarkColor.Green]:  'Zelena',
  [QueenMarkColor.Blue]:   'Plava',
}

export enum QueenOrigin {
  Purchased   = 1,
  OwnBreeding = 2,
  Swarm       = 3,
  Supersedure = 4,
  Unknown     = 99,
}

export const QueenOriginLabels: Record<QueenOrigin, string> = {
  [QueenOrigin.Purchased]:   'Kupljena',
  [QueenOrigin.OwnBreeding]: 'Vlastiti uzgoj',
  [QueenOrigin.Swarm]:       'Rojenje',
  [QueenOrigin.Supersedure]: 'Tiha zamjena',
  [QueenOrigin.Unknown]:     'Nepoznato',
}

export enum QueenStatus {
  Active   = 1,
  Replaced = 2,
  Died     = 3,
  Missing  = 4,
}

export const QueenStatusLabels: Record<QueenStatus, string> = {
  [QueenStatus.Active]:   'Aktivna',
  [QueenStatus.Replaced]: 'Zamijenjena',
  [QueenStatus.Died]:     'Uginula',
  [QueenStatus.Missing]:  'Nestala',
}

export interface Queen {
  id: number
  year: number
  markColor: QueenMarkColor
  markColorName: string
  isMarked: boolean
  isClipped: boolean
  origin: QueenOrigin
  originName: string
  status: QueenStatus
  statusName: string
  introducedDate: string
  endDate?: string
  notes?: string
  beehiveId: number
  createdAt: string
}

export interface CreateQueenPayload {
  year: number
  /** Omitted/null → backend derives the color from the year. */
  markColor?: QueenMarkColor | null
  isMarked: boolean
  isClipped: boolean
  origin: QueenOrigin
  introducedDate: string
  notes?: string
}

export interface UpdateQueenPayload {
  year: number
  markColor: QueenMarkColor
  isMarked: boolean
  isClipped: boolean
  origin: QueenOrigin
  status: QueenStatus
  introducedDate: string
  endDate?: string | null
  notes?: string
}

/** One field-level correction made to a queen record. */
export interface QueenEditLog {
  id: number
  fieldLabel: string
  oldValue?: string
  newValue?: string
  editedAt: string
  editedByName?: string
}

// ── Todo ──────────────────────────────────────────────────────────────────────

export enum TodoPriority {
  Low    = 1,
  Medium = 2,
  High   = 3,
}

export const TodoPriorityLabels: Record<TodoPriority, string> = {
  [TodoPriority.Low]:    'Nizak',
  [TodoPriority.Medium]: 'Srednji',
  [TodoPriority.High]:   'Visok',
}

export interface AssignableUser {
  id: number
  fullName: string
}

export interface Todo {
  id: number
  title: string
  notes?: string
  dueDate?: string
  priority: TodoPriority
  priorityName: string
  isCompleted: boolean
  completedAt?: string
  apiaryId?: number
  beehiveId?: number
  createdByName?: string
  assignedToId?: number
  assignedToName?: string
  createdAt: string
}

export interface CreateTodoPayload {
  title: string
  notes?: string
  dueDate?: string | null
  priority: TodoPriority
  assignedToId?: number | null
  apiaryId?: number
  beehiveId?: number
}

export interface UpdateTodoPayload {
  title: string
  notes?: string
  dueDate?: string | null
  priority: TodoPriority
  isCompleted: boolean
  assignedToId?: number | null
}

// ── Diet ──────────────────────────────────────────────────────────────────────

export enum DietStatus {
  NotStarted   = 1,
  InProgress   = 2,
  Completed    = 3,
  StoppedEarly = 4,
}

export enum FeedingEntryStatus {
  Pending   = 1,
  Completed = 2,
}

export enum DietReason {
  LackOfFood               = 1,
  WinterFeeding            = 2,
  SpringStimulation        = 3,
  NewSwarmSupport          = 4,
  PostHarvestRecovery      = 5,
  DroughtConditions        = 6,
  WeakColonySupport        = 7,
  QueenIntroductionSupport = 8,
  Custom                   = 9,
}

export const DietReasonLabels: Record<DietReason, string> = {
  [DietReason.LackOfFood]:               'Nedostatak hrane',
  [DietReason.WinterFeeding]:            'Zimsko hranjenje',
  [DietReason.SpringStimulation]:        'Proljetna stimulacija',
  [DietReason.NewSwarmSupport]:          'Podrška novom roju',
  [DietReason.PostHarvestRecovery]:      'Oporavak nakon berbe',
  [DietReason.DroughtConditions]:        'Uvjeti suše',
  [DietReason.WeakColonySupport]:        'Podrška slaboj koloniji',
  [DietReason.QueenIntroductionSupport]: 'Podrška uvođenju matice',
  [DietReason.Custom]:                   'Vlastito',
}

export enum FoodType {
  SugarSyrup     = 1,
  Fondant        = 2,
  Pollen         = 3,
  ProteinPatties = 4,
  Custom         = 5,
}

export const FoodTypeLabels: Record<FoodType, string> = {
  [FoodType.SugarSyrup]:     'Šećerni sirup',
  [FoodType.Fondant]:        'Fondan',
  [FoodType.Pollen]:         'Polen',
  [FoodType.ProteinPatties]: 'Proteinski kolači',
  [FoodType.Custom]:         'Vlastito',
}

export enum FeedingAmountUnit {
  Litre      = 1,
  Millilitre = 2,
  Kilogram   = 3,
  Gram       = 4,
}

export const FeedingAmountUnitLabels: Record<FeedingAmountUnit, string> = {
  [FeedingAmountUnit.Litre]:      'L',
  [FeedingAmountUnit.Millilitre]: 'ml',
  [FeedingAmountUnit.Kilogram]:   'kg',
  [FeedingAmountUnit.Gram]:       'g',
}

export interface FeedingEntry {
  id: number
  scheduledDate: string
  status: FeedingEntryStatus
  statusName: string
  completionDate?: string
  /** Optional note recorded when the round was ticked ("košnice 7 i 12 preskočene"). */
  note?: string
  dietId: number
}

/** One hive's membership in a programme. Removed links stay for history. */
export interface DietBeehive {
  id: number
  beehiveId: number
  beehiveName: string
  addedOn: string
  /** Null while the hive is still on the programme. */
  removedOn?: string
}

/**
 * A feeding programme. Apiary-scoped since SPEC-12: it covers a set of the apiary's hives
 * (`beehives` on the detail view) rather than belonging to one hive.
 */
export interface Diet {
  id: number
  apiaryId: number
  apiaryName?: string
  name: string
  startDate: string
  reason: DietReason
  reasonName: string
  customReason?: string
  durationDays: number
  frequencyDays: number
  foodType: FoodType
  foodTypeName: string
  customFoodType?: string
  amountPerHive?: number
  amountUnit?: FeedingAmountUnit
  amountUnitName?: string
  amountNote?: string
  status: DietStatus
  statusName: string
  earlyCompletionComment?: string
  /** Hives currently on the programme — removed ones are never counted. */
  hiveCount: number
  totalEntries: number
  completedEntries: number
  /** Earliest pending round from today on; else the earliest overdue one. */
  nextFeedingDate?: string
  createdByName?: string
  createdAt: string

  // ── Feeding cost (SPEC-12 Phase E) ──────────────────────────────────────────
  /** Exact planned consumption over ALL rounds; undefined when no amount was recorded. */
  plannedAmount?: number
  /** Same fold, but only over completed rounds — how much has actually been used so far. */
  consumedAmount?: number
  /**
   * Attributed cost, grouped by currency. Undefined both when nothing has been attributed yet and
   * when the caller is a Beekeeper — the server omits it, so there is nothing to distinguish here.
   */
  costTotals?: CostTotal[]
  /** Total ÷ active hive count — only present when there is exactly one currency and hiveCount > 0. */
  costPerHive?: number
}

export interface CostTotal {
  currency: string
  total: number
}

export interface DietDetail extends Diet {
  feedingEntries: FeedingEntry[]
  /** Every link, active and removed. */
  beehives: DietBeehive[]
}

/** One (hive × active programme) pair from GET /feedings/active. */
export interface DietActiveInfo {
  beehiveId: number
  dietId: number
  dietName: string
  foodType: FoodType
  foodTypeName: string
  customFoodType?: string
  amountPerHive?: number
  amountUnit?: FeedingAmountUnit
  amountUnitName?: string
  amountNote?: string
  startDate: string
  nextFeedingDate?: string
  completedRounds: number
  totalRounds: number
}

export interface CreateDietPayload {
  apiaryId: number
  name: string
  startDate: string
  reason: DietReason
  customReason?: string
  durationDays: number
  frequencyDays: number
  foodType: FoodType
  customFoodType?: string
  amountPerHive?: number | null
  amountUnit?: FeedingAmountUnit | null
  amountNote?: string | null
  beehiveIds: number[]
}

/**
 * Update deliberately carries no hive list: DietBeehive rows hold removal history, so replacing the
 * set on every edit would destroy it. Hives are managed through their own endpoints.
 */
export interface UpdateDietPayload {
  name: string
  startDate: string
  reason: DietReason
  customReason?: string
  durationDays: number
  frequencyDays: number
  foodType: FoodType
  customFoodType?: string
  amountPerHive?: number | null
  amountUnit?: FeedingAmountUnit | null
  amountNote?: string | null
}

export interface CompleteEarlyPayload {
  comment: string
}

export interface AddDietBeehivesPayload {
  beehiveIds: number[]
}

export interface CompleteFeedingEntryPayload {
  note?: string | null
}

// ── Calendar ──────────────────────────────────────────────────────────────────

export interface CalendarTodo {
  id: number
  title: string
  notes?: string
  dueDate?: string
  priority: TodoPriority
  priorityName: string
  isCompleted: boolean
  apiaryId?: number
  apiaryName?: string
  beehiveId?: number
  beehiveName?: string
}

/** A feeding round covers every hive on the programme, so it carries the apiary and a count. */
export interface CalendarFeedingEntry {
  id: number
  scheduledDate: string
  status: FeedingEntryStatus
  statusName: string
  dietId: number
  dietName: string
  apiaryId: number
  apiaryName: string
  hiveCount: number
  foodTypeName: string
}

export interface CalendarEventsResponse {
  todos: CalendarTodo[]
  feedingEntries: CalendarFeedingEntry[]
}

// Calendar sync (SPEC-11)
export interface CalendarFeedInfo {
  url: string
  enabled: boolean
}

export interface CalendarSettings {
  feedEnabled: boolean
  syncFeedings: boolean
  syncTodos: boolean
  syncTreatments: boolean
  syncInspections: boolean
  dailyAgendaEnabled: boolean
}

// ── Admin ─────────────────────────────────────────────────────────────────────

export interface AdminOrganization {
  id: number
  name: string
  description?: string
  userCount: number
  apiaryCount: number
  createdByName?: string
  createdAt: string
  // Subscription plan (SPEC-09)
  plan: PlanType
  planName: string
  planValidUntil?: string | null
  planNotes?: string | null
}

export interface UpdateOrganizationPlanPayload {
  plan: PlanType
  planValidUntil?: string | null
  planNotes?: string | null
}

export interface CreateOrganizationPayload {
  name: string
  description?: string
}

export interface UpdateOrganizationPayload {
  name: string
  description?: string
}

export interface AdminUser {
  id: number
  firstName: string
  lastName: string
  email: string
  /** Canonical E.164. Null on accounts created before phone numbers existed. */
  phone: string | null
  role: string
  organizationId?: number
  organizationName?: string
  apiaryId?: number
  apiaryName?: string
  assignedBeehiveIds: number[]
  createdAt: string
}

export interface AdminApiaryListItem {
  id: number
  name: string
}

export interface AdminBeehiveListItem {
  id: number
  name: string
  apiaryName: string
}

export interface CreateAdminUserPayload {
  firstName: string
  lastName: string
  email: string
  phone: string
  password: string
  role: string
  organizationId?: number | null
  apiaryId?: number | null
  assignedBeehiveIds: number[]
}

export interface UpdateAdminUserPayload {
  firstName: string
  lastName: string
  email: string
  /** Blank leaves the stored number unchanged — it never clears it. */
  phone?: string
  role: string
  organizationId?: number | null
  apiaryId?: number | null
  assignedBeehiveIds: number[]
}

// ── Org Management ────────────────────────────────────────────────────────────

export interface OrgMember {
  id: number
  firstName: string
  lastName: string
  email: string
  /** Canonical E.164. Null on accounts created before phone numbers existed. */
  phone: string | null
  role: string
  apiaryId?: number
  apiaryName?: string
  assignedBeehiveIds: number[]
  assignedBeehiveNames: string[]
}

export interface OrgAvailableBeehive {
  id: number
  name: string
  apiaryName: string
}

export interface OrgAvailableApiary {
  id: number
  name: string
}

export interface UpdateBeehiveAssignmentsPayload {
  beehiveIds: number[]
}

export interface UpdateApiaryAssignmentPayload {
  apiaryId: number | null
}

export interface CreateOrgMemberPayload {
  firstName: string
  lastName: string
  email: string
  phone: string
  password: string
  role: string
  apiaryId?: number | null
  assignedBeehiveIds: number[]
}

// ── Expenses ──────────────────────────────────────────────────────────────────

export enum ExpenseSource {
  Manual      = 1,
  ReceiptScan = 2,
}

export const ExpenseSourceLabels: Record<ExpenseSource, string> = {
  [ExpenseSource.Manual]:      'Ručno',
  [ExpenseSource.ReceiptScan]: 'Skeniranje računa',
}

export interface ExpenseItem {
  id: number
  name: string
  quantity: number
  unit?: string
  unitPrice: number
  totalPrice: number
  sortOrder: number
  /** Optional attribution to a feeding programme (SPEC-12 Phase E). */
  dietId?: number
}

export interface Expense {
  id: number
  source: ExpenseSource
  sourceName: string
  purchaseDate: string
  totalAmount: number
  currency: string
  notes?: string
  itemCount: number
  createdByName?: string
  createdAt: string
}

export interface ExpenseDetail extends Expense {
  items: ExpenseItem[]
}

export interface CreateExpenseItemPayload {
  name: string
  quantity: number
  unit?: string
  unitPrice: number
  totalPrice: number
  sortOrder: number
  /**
   * Optional attribution to a feeding programme (SPEC-12 Phase E). Must always be carried through
   * on every edit — the backend replaces the whole item collection on update, so if this is dropped
   * from even one PUT, every attribution on that receipt is silently wiped, no error.
   */
  dietId?: number | null
}

export interface CreateExpensePayload {
  source: ExpenseSource
  purchaseDate: string
  totalAmount: number
  currency: string
  notes?: string
  items: CreateExpenseItemPayload[]
}

export interface UpdateExpensePayload {
  purchaseDate: string
  totalAmount: number
  currency: string
  notes?: string
  items: CreateExpenseItemPayload[]
}

// ── Harvests (SPEC-02) ──────────────────────────────────────────────────────────

export enum HoneyType {
  Acacia    = 1,
  Linden    = 2,
  Chestnut  = 3,
  Sunflower = 4,
  Meadow    = 5,
  Forest    = 6,
  Rapeseed  = 7,
  Other     = 99,
}

export const HoneyTypeLabels: Record<HoneyType, string> = {
  [HoneyType.Acacia]:    'Bagrem',
  [HoneyType.Linden]:    'Lipa',
  [HoneyType.Chestnut]:  'Kesten',
  [HoneyType.Sunflower]: 'Suncokret',
  [HoneyType.Meadow]:    'Livadski',
  [HoneyType.Forest]:    'Šumski',
  [HoneyType.Rapeseed]:  'Uljana repica',
  [HoneyType.Other]:     'Ostalo',
}

export interface HarvestEntry {
  id: number
  beehiveId: number
  beehiveName?: string
  quantityKg: number
  framesExtracted?: number
}

export interface Harvest {
  id: number
  apiaryId: number
  apiaryName?: string
  date: string
  honeyType: HoneyType
  honeyTypeName: string
  pricePerKg?: number
  notes?: string
  totalKg: number
  entryCount: number
  estimatedRevenue?: number
  createdByName?: string
  createdAt: string
}

export interface HarvestDetail extends Harvest {
  entries: HarvestEntry[]
}

export interface CreateHarvestEntryPayload {
  beehiveId: number
  quantityKg: number
  framesExtracted?: number | null
}

export interface CreateHarvestPayload {
  apiaryId: number
  date: string
  honeyType: HoneyType
  pricePerKg?: number | null
  notes?: string
  entries: CreateHarvestEntryPayload[]
}

/** Update shares the create shape except the apiary, which is immutable after creation. */
export interface UpdateHarvestPayload {
  date: string
  honeyType: HoneyType
  pricePerKg?: number | null
  notes?: string
  entries: CreateHarvestEntryPayload[]
}

/** Per-hive honey yield (from /harvests/hive/{id}/yield). */
export interface HiveYield {
  currentSeasonKg: number
  byYear: { year: number; kg: number }[]
}

// ── Treatments (SPEC-08) ──────────────────────────────────────────────────────────

export enum TreatmentPurpose {
  Varroa     = 1,
  Nosema     = 2,
  Chalkbrood = 3,
  Other      = 99,
}

export const TreatmentPurposeLabels: Record<TreatmentPurpose, string> = {
  [TreatmentPurpose.Varroa]:     'Varoa',
  [TreatmentPurpose.Nosema]:     'Nozemoza',
  [TreatmentPurpose.Chalkbrood]: 'Krečno leglo',
  [TreatmentPurpose.Other]:      'Ostalo',
}

export enum ActiveSubstance {
  Amitraz        = 1,
  Flumethrin     = 2,
  TauFluvalinate = 3,
  Coumaphos      = 4,
  OxalicAcid     = 5,
  FormicAcid     = 6,
  Thymol         = 7,
  Other          = 99,
}

export const ActiveSubstanceLabels: Record<ActiveSubstance, string> = {
  [ActiveSubstance.Amitraz]:        'Amitraz',
  [ActiveSubstance.Flumethrin]:     'Flumetrin',
  [ActiveSubstance.TauFluvalinate]: 'Tau-fluvalinat',
  [ActiveSubstance.Coumaphos]:      'Kumafos',
  [ActiveSubstance.OxalicAcid]:     'Oksalna kiselina',
  [ActiveSubstance.FormicAcid]:     'Mravlja kiselina',
  [ActiveSubstance.Thymol]:         'Timol',
  [ActiveSubstance.Other]:          'Ostalo',
}

export enum ApplicationMethod {
  Strips      = 1,
  Trickling   = 2,
  Sublimation = 3,
  Evaporation = 4,
  InFeed      = 5,
  Spraying    = 6,
  Other       = 99,
}

export const ApplicationMethodLabels: Record<ApplicationMethod, string> = {
  [ApplicationMethod.Strips]:      'Trake',
  [ApplicationMethod.Trickling]:   'Nakapavanje',
  [ApplicationMethod.Sublimation]: 'Sublimacija',
  [ApplicationMethod.Evaporation]: 'Isparavanje',
  [ApplicationMethod.InFeed]:      'U prihrani',
  [ApplicationMethod.Spraying]:    'Prskanje',
  [ApplicationMethod.Other]:       'Ostalo',
}

export enum TreatmentStatus {
  InProgress = 1,
  Karenca    = 2,
  Completed  = 3,
}

export const TreatmentStatusLabels: Record<TreatmentStatus, string> = {
  [TreatmentStatus.InProgress]: 'U toku',
  [TreatmentStatus.Karenca]:    'Karenca',
  [TreatmentStatus.Completed]:  'Završen',
}

export interface TreatmentEntry {
  id: number
  beehiveId: number
  beehiveName?: string
  doseNote?: string
}

export interface Treatment {
  id: number
  apiaryId: number
  apiaryName?: string
  purpose: TreatmentPurpose
  purposeName: string
  productName: string
  activeSubstance: ActiveSubstance
  activeSubstanceName: string
  method: ApplicationMethod
  methodName: string
  dosePerHive: string
  startDate: string
  endDate?: string
  withdrawalDays: number
  batchNumber?: string
  supplier?: string
  notes?: string
  karencaUntil: string
  status: TreatmentStatus
  statusName: string
  hiveCount: number
  hiveNames: string[]
  createdByName?: string
  createdAt: string
}

export interface TreatmentDetail extends Treatment {
  entries: TreatmentEntry[]
}

export interface CreateTreatmentEntryPayload {
  beehiveId: number
  doseNote?: string | null
}

export interface CreateTreatmentPayload {
  apiaryId: number
  purpose: TreatmentPurpose
  productName: string
  activeSubstance: ActiveSubstance
  method: ApplicationMethod
  dosePerHive: string
  startDate: string
  endDate?: string | null
  withdrawalDays: number
  batchNumber?: string | null
  supplier?: string | null
  notes?: string | null
  entries: CreateTreatmentEntryPayload[]
}

/** Update shares the create shape except the apiary, which is immutable after creation. */
export interface UpdateTreatmentPayload {
  purpose: TreatmentPurpose
  productName: string
  activeSubstance: ActiveSubstance
  method: ApplicationMethod
  dosePerHive: string
  startDate: string
  endDate?: string | null
  withdrawalDays: number
  batchNumber?: string | null
  supplier?: string | null
  notes?: string | null
  entries: CreateTreatmentEntryPayload[]
}

// ── AI Advisor (SPEC-01) ────────────────────────────────────────────────────────

export type AdvisorRole = 'User' | 'Assistant'

export interface AdvisorMessage {
  id: number
  role: AdvisorRole
  content: string
  createdAt: string
}

export interface AdvisorConversationSummary {
  id: number
  title: string
  beehiveId?: number
  beehiveName?: string
  lastMessageAt: string
  createdAt: string
}

export interface AdvisorConversationDetail extends AdvisorConversationSummary {
  messages: AdvisorMessage[]
}

export interface AdvisorMessagePair {
  userMessage: AdvisorMessage
  assistantMessage: AdvisorMessage
}

export interface CreateConversationPayload {
  beehiveId?: number | null
  message: string
}

export interface SendMessagePayload {
  message: string
}

// ── Learning module (SPEC-06) ───────────────────────────────────────────────────

export enum LearningCategory {
  Osnove            = 1,
  SezonskiRadovi    = 2,
  BolestiINametnici = 3,
  Oprema            = 4,
  Propisi           = 5,
  Napredno          = 99,
}

export const LearningCategoryLabels: Record<LearningCategory, string> = {
  [LearningCategory.Osnove]:            'Osnove',
  [LearningCategory.SezonskiRadovi]:    'Sezonski radovi',
  [LearningCategory.BolestiINametnici]: 'Bolesti i nametnici',
  [LearningCategory.Oprema]:            'Oprema',
  [LearningCategory.Propisi]:           'Propisi',
  [LearningCategory.Napredno]:          'Napredno',
}

export const MonthLabels = [
  'Januar', 'Februar', 'Mart', 'April', 'Maj', 'Juni',
  'Juli', 'August', 'Septembar', 'Oktobar', 'Novembar', 'Decembar',
] as const

export interface LearningTopicSummary {
  id: number
  title: string
  category: LearningCategory
  categoryName: string
  months?: number[] | null
  summary: string
  videoUrl?: string | null
  fileUrl?: string | null
  fileName?: string | null
  isRead: boolean
  publishedAt?: string
}

export interface LearningTopicDetail extends LearningTopicSummary {
  bodyMarkdown: string
}

export interface AdminLearningTopic {
  id: number
  title: string
  category: LearningCategory
  categoryName: string
  months?: number[] | null
  summary: string
  bodyMarkdown: string
  videoUrl?: string | null
  fileUrl?: string | null
  fileName?: string | null
  isPublished: boolean
  publishedAt?: string
  createdAt: string
  updatedAt?: string
}

export interface SaveLearningTopicPayload {
  title: string
  category: LearningCategory
  months?: number[] | null
  summary: string
  bodyMarkdown: string
  videoUrl?: string | null
  fileUrl?: string | null
  fileName?: string | null
}

export interface GenerateDraftPayload {
  title: string
  outline?: string | null
}

export interface LearningDraft {
  bodyMarkdown: string
  summary: string
}

// ── Pastures & apiary moves (SPEC-10) ───────────────────────────────────────────

export interface Pasture {
  id: number
  name: string
  latitude?: number | null
  longitude?: number | null
  address?: string | null
  floraNotes?: string | null
  notes?: string | null
  apiariesOnPasture: number
  createdAt: string
}

export interface SavePasturePayload {
  name: string
  latitude?: number | null
  longitude?: number | null
  address?: string | null
  floraNotes?: string | null
  notes?: string | null
}

export interface ApiaryMove {
  id: number
  apiaryId: number
  fromPastureId?: number | null
  /** Null = selidba s matične lokacije. */
  fromPastureName?: string | null
  /** Null = ova selidba je vratila pčelinjak na matičnu lokaciju. */
  toPastureId: number | null
  toPastureName: string
  movedAt: string
  certificateNumber?: string | null
  notes?: string | null
  createdByName?: string | null
  createdAt: string
}

export interface CreateApiaryMovePayload {
  toPastureId: number
  movedAt: string
  certificateNumber?: string | null
  notes?: string | null
}

// ── API Error shape ───────────────────────────────────────────────────────────

export interface ApiError {
  type: string
  title: string
  status: number
  errors: Record<string, string[]>
}

// ── Plans & billing (SPEC-09) ───────────────────────────────────────────────────

export enum PlanType {
  Free     = 1,
  Standard = 2,
  Pro      = 3,
  Max      = 4,
  /** Hidden plan — never shown in public plan lists; assigned only by SystemAdmin. */
  Partner  = 5,
}

export const PlanTypeLabels: Record<PlanType, string> = {
  [PlanType.Free]:     'Besplatni',
  [PlanType.Standard]: 'Standard',
  [PlanType.Pro]:      'Pro',
  [PlanType.Max]:      'Max',
  [PlanType.Partner]:  'Partner',
}

export interface PlanUsage {
  apiaries: number
  apiariesLimit?: number | null
  beehives: number
  beehivesLimit?: number | null
  members: number
  membersLimit?: number | null
  advisorMessagesThisMonth: number
  advisorMessagesLimit?: number | null
  /** AI assistant commands used this month (SPEC-17); 0 = no access, null = unlimited. */
  aiCommandsThisMonth: number
  aiCommandsLimit?: number | null
}

export interface MyPlan {
  plan: PlanType
  planName: string
  effectivePlan: PlanType
  effectivePlanName: string
  planValidUntil?: string | null
  planNotes?: string | null
  usage: PlanUsage
}

// ── User feedback (SPEC-13) ─────────────────────────────────────────────────────

export enum FeedbackType {
  Bug            = 1,
  Complaint      = 2,
  Compliment     = 3,
  FeatureRequest = 4,
  Question       = 5,
  Other          = 6,
}

export const FeedbackTypeLabels: Record<FeedbackType, string> = {
  [FeedbackType.Bug]:            'Prijava problema',
  [FeedbackType.Complaint]:      'Žalba',
  [FeedbackType.Compliment]:     'Pohvala',
  [FeedbackType.FeatureRequest]: 'Prijedlog',
  [FeedbackType.Question]:       'Pitanje',
  [FeedbackType.Other]:          'Ostalo',
}

/** One-line hint under each option in the form, so people pick the right category. */
export const FeedbackTypeHints: Record<FeedbackType, string> = {
  [FeedbackType.Bug]:            'Nešto ne radi kako treba ili prikazuje grešku',
  [FeedbackType.Complaint]:      'Radi, ali vam otežava posao',
  [FeedbackType.Compliment]:     'Nešto vam se posebno svidjelo',
  [FeedbackType.FeatureRequest]: 'Ideja za nešto novo ili bolje',
  [FeedbackType.Question]:       'Ne znate kako nešto funkcioniše',
  [FeedbackType.Other]:          'Sve ostalo',
}

export const FeedbackTypeEmojis: Record<FeedbackType, string> = {
  [FeedbackType.Bug]:            '🐞',
  [FeedbackType.Complaint]:      '😕',
  [FeedbackType.Compliment]:     '💛',
  [FeedbackType.FeatureRequest]: '💡',
  [FeedbackType.Question]:       '❓',
  [FeedbackType.Other]:          '✉️',
}

export enum FeedbackSeverity {
  Low      = 1,
  Medium   = 2,
  High     = 3,
  Critical = 4,
}

export const FeedbackSeverityLabels: Record<FeedbackSeverity, string> = {
  [FeedbackSeverity.Low]:      'Nizak',
  [FeedbackSeverity.Medium]:   'Srednji',
  [FeedbackSeverity.High]:     'Visok',
  [FeedbackSeverity.Critical]: 'Kritičan',
}

export enum FeedbackStatus {
  New       = 1,
  InReview  = 2,
  Resolved  = 3,
  Dismissed = 4,
}

export const FeedbackStatusLabels: Record<FeedbackStatus, string> = {
  [FeedbackStatus.New]:       'Novo',
  [FeedbackStatus.InReview]:  'U razmatranju',
  [FeedbackStatus.Resolved]:  'Riješeno',
  [FeedbackStatus.Dismissed]: 'Odbijeno',
}

export interface Feedback {
  id: number
  type: FeedbackType
  typeName: string
  severity?: FeedbackSeverity | null
  severityName?: string | null
  subject: string
  message: string
  pageContext?: string | null
  hasScreenshot: boolean
  status: FeedbackStatus
  statusName: string
  adminResponse?: string | null
  respondedAt?: string | null
  createdAt: string
}

export interface AdminFeedback extends Feedback {
  userAgent?: string | null
  userId?: number | null
  submitterName?: string | null
  submitterEmail?: string | null
  organizationName?: string | null
}

export interface CreateFeedbackPayload {
  type: FeedbackType
  severity?: FeedbackSeverity | null
  subject: string
  message: string
  pageContext?: string | null
  userAgent?: string | null
}

export interface UpdateFeedbackStatusPayload {
  status: FeedbackStatus
  adminResponse?: string | null
}

export interface FeedbackSummary {
  newCount: number
}

// ── Invite a friend (SPEC-15) ───────────────────────────────────────────────────

export enum InvitationSource {
  EmailInvite = 1,
  ShareLink   = 2,
}

export const InvitationSourceLabels: Record<InvitationSource, string> = {
  [InvitationSource.EmailInvite]: 'E-pošta',
  [InvitationSource.ShareLink]:   'Link',
}

export enum InvitationStatus {
  Sent     = 1,
  Accepted = 2,
}

export interface Invitation {
  id: number
  source: InvitationSource
  sourceName: string
  email: string
  status: InvitationStatus
  /**
   * Bosnian label from the backend. Three states out of two stored values: an invitee who
   * registered but has not confirmed their address yet reads "Registrovao se — čeka potvrdu",
   * because that is exactly what the inviter's reward is waiting on. Render it as-is.
   */
  statusName: string
  rewardDays?: number | null
  acceptedAt?: string | null
  createdAt: string
}

export interface InvitationSummary {
  sentCount: number
  acceptedCount: number
  rewardDaysEarned: number
  rewardDaysRemaining: number
  /** Built server-side — the client cannot know FrontendUrl, which differs from the browser origin. */
  shareUrl: string
  inviteeTrialDays: number
  rewardDaysPerInvite: number
}

/** First name only — the /register banner never shows a surname or an address. */
export interface ReferralInviter {
  inviterFirstName: string
  trialDays: number
}

// ── AI Assistant (SPEC-17) ──────────────────────────────────────────────────────

/** Why a proposal card cannot be confirmed as-is. "None" means the target resolved. */
export type AssistantIssue =
  | 'None'
  | 'ApiaryNotFound'
  | 'ApiaryAmbiguous'
  | 'HiveNotFound'
  | 'HiveAmbiguous'
  | 'NoHivesInApiary'
  | 'MissingHive'
  | 'MissingTitle'
  | 'TodoNotFound'
  | 'TodoAmbiguous'
  | 'InspectionNotFound'
  | 'InspectionAmbiguous'

export type AssistantActionStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'Failed'

export interface AssistantFields {
  /** ISO date, "yyyy-MM-dd" — a calendar date, not an instant. */
  date?: string | null
  honeyLevel?: HoneyLevel | null
  broodStatus?: string | null
  notes?: string | null
  title?: string | null
  priority?: TodoPriority | null
  dueDate?: string | null
}

export type AssistantActionKind =
  | 'CreateInspection' | 'CreateTodo'
  | 'UpdateTodo' | 'CompleteTodo' | 'UpdateInspection' | 'DeleteTodo' | 'DeleteInspection'

export interface AssistantAction {
  id: number
  kind: AssistantActionKind
  kindLabel: string
  status: AssistantActionStatus
  targetSummary: string
  issue: AssistantIssue
  unresolvedHiveNumbers: string[]
  apiaryId?: number | null
  apiaryName?: string | null
  beehiveId?: number | null
  beehiveName?: string | null
  fields: AssistantFields
  resultEntityType?: string | null
  resultEntityId?: number | null
  errorMessage?: string | null
  /** Phase C: true for update/complete/delete — needs a second, separate confirmation. */
  isDestructive: boolean
  /** Phase C: the existing record's current values, for an old → new comparison on the card. */
  previousFields?: AssistantFields | null
}

/** A tappable follow-up option (Phase B). Tapping sends `text` as the next turn's message. */
export interface AssistantCandidate {
  label: string
  text: string
}

export interface AssistantTurn {
  id: number
  role: 'User' | 'Assistant'
  content: string
  transcript?: string | null
  createdAt: string
  actions: AssistantAction[]
  /** Non-empty only on the latest assistant turn, and only when something needed clarifying. */
  candidates: AssistantCandidate[]
}

export interface AssistantSessionSummary {
  id: number
  title: string
  lastActivityAt: string
  createdAt: string
}

export interface AssistantSessionDetail extends AssistantSessionSummary {
  turns: AssistantTurn[]
}

export interface AssistantExecutionResult {
  actionId: number
  status: AssistantActionStatus
  targetSummary: string
  resultEntityType?: string | null
  resultEntityId?: number | null
  errorMessage?: string | null
}

export interface AssistantConfirmResponse {
  message: string
  results: AssistantExecutionResult[]
}

export interface StartAssistantSessionPayload {
  text: string
  transcript?: string | null
  apiaryId?: number | null
  beehiveId?: number | null
}

export interface AssistantTurnPayload {
  text: string
  transcript?: string | null
  apiaryId?: number | null
  beehiveId?: number | null
}

export interface ConfirmActionItem {
  id: number
  apiaryId?: number | null
  beehiveId?: number | null
  fields: AssistantFields
}

export interface ConfirmActionsPayload {
  actions: ConfirmActionItem[]
}
