import { AnnouncementType } from '../../core/models'

/**
 * Badge colours per announcement type (SPEC-21 D7). Kept beside the components that render the
 * badge — the banner, the modal and the "Šta je novo" page must not drift apart on colour.
 */
export const ANNOUNCEMENT_TYPE_CLASS: Record<AnnouncementType, string> = {
  [AnnouncementType.New]:
    'text-honey-700 bg-honey-100 dark:text-honey-300 dark:bg-honey-500/15',
  [AnnouncementType.Improvement]:
    'text-sky-700 bg-sky-100 dark:text-sky-300 dark:bg-sky-500/15',
  [AnnouncementType.Fix]:
    'text-emerald-700 bg-emerald-100 dark:text-emerald-300 dark:bg-emerald-500/15',
}
