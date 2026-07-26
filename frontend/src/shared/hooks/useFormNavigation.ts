import { useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { canGoBack, previousPathname } from '../utils/historyStack'

/**
 * Navigation for pages that are a *step* rather than a destination: create/edit forms, the receipt
 * scanner, the member assignment screen, and any page that deletes the thing it shows.
 *
 * Such a page must not survive in history. Before this, saving a košnica pushed the hive detail on
 * top of the form, so Back returned to the form the user had just submitted — and for a create form
 * that meant an empty form, one submit away from a duplicate record.
 *
 * @param fallback where to land when there is nothing to go back to (deep link, fresh tab, or a
 *                 history mirror wiped by a reload).
 */
export function useFormNavigation(fallback: string) {
  const navigate = useNavigate()

  /**
   * Cancel, and the back affordance in `FormHeader`. Real browser back, so the page behind keeps
   * its filters and scroll position instead of being re-mounted from scratch.
   */
  const goBack = useCallback(() => {
    if (canGoBack()) navigate(-1)
    else navigate(fallback, { replace: true })
  }, [navigate, fallback])

  /**
   * Leave for `target` after a successful save or delete, consuming this page's history entry.
   *
   * Going back is preferred over replacing when `target` is already the previous entry (saving an
   * edit, or a list-based form returning to its list): replacing would leave two adjacent entries
   * for the same page, and the user's first Back press would appear to do nothing. Only the pathname
   * is compared, so returning to a filtered list keeps the filter the user came in with.
   */
  const goAfterSave = useCallback(
    (target: string) => {
      const targetPathname = target.split(/[?#]/)[0]
      if (previousPathname() === targetPathname) navigate(-1)
      else navigate(target, { replace: true })
    },
    [navigate],
  )

  return { goBack, goAfterSave }
}
