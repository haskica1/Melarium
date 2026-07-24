import { ActiveSubstance, ApplicationMethod, TreatmentPurpose } from '../../core/models'

export interface TreatmentPreset {
  label: string
  purpose: TreatmentPurpose
  productName: string
  activeSubstance: ActiveSubstance
  method: ApplicationMethod
  dosePerHive: string
  withdrawalDays: number
}

/**
 * Common registered varroa products — selecting one pre-fills the form (all fields stay editable).
 * Withdrawal (karenca) is 0 for these registered bee products; the user adjusts if their label differs.
 */
export const TREATMENT_PRESETS: TreatmentPreset[] = [
  { label: 'Apivar (amitraz, trake)',           purpose: TreatmentPurpose.Varroa, productName: 'Apivar',       activeSubstance: ActiveSubstance.Amitraz,    method: ApplicationMethod.Strips,      dosePerHive: '2 trake po košnici',                 withdrawalDays: 0 },
  { label: 'Bayvarol (flumetrin, trake)',       purpose: TreatmentPurpose.Varroa, productName: 'Bayvarol',     activeSubstance: ActiveSubstance.Flumethrin, method: ApplicationMethod.Strips,      dosePerHive: '4 trake po košnici',                 withdrawalDays: 0 },
  { label: 'Apiguard (timol, isparavanje)',     purpose: TreatmentPurpose.Varroa, productName: 'Apiguard',     activeSubstance: ActiveSubstance.Thymol,     method: ApplicationMethod.Evaporation, dosePerHive: '1 posudica (50 g), 2 tretmana',      withdrawalDays: 0 },
  { label: 'Oksalna kiselina — nakapavanje',    purpose: TreatmentPurpose.Varroa, productName: 'Oksalna kiselina 3.2%', activeSubstance: ActiveSubstance.OxalicAcid, method: ApplicationMethod.Trickling, dosePerHive: '5 ml po ulici pčela', withdrawalDays: 0 },
  { label: 'Oksalna kiselina — sublimacija',    purpose: TreatmentPurpose.Varroa, productName: 'Oksalna kiselina',      activeSubstance: ActiveSubstance.OxalicAcid, method: ApplicationMethod.Sublimation, dosePerHive: '2 g po košnici',        withdrawalDays: 0 },
  { label: 'Mravlja kiselina — isparavanje',    purpose: TreatmentPurpose.Varroa, productName: 'Mravlja kiselina 60%',  activeSubstance: ActiveSubstance.FormicAcid, method: ApplicationMethod.Evaporation, dosePerHive: '30 ml po tretmanu',    withdrawalDays: 0 },
]
