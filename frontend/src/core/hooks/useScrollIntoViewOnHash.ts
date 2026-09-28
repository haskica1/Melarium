import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

/**
 * Scrolls the element with this id into view when the URL's hash names it and its content is ready.
 * E-mail footers link straight to a profile section ("/profile#obavjestenja", ADR-048); the browser's
 * own jump happens before a section that loads its data has its final place on the page.
 */
export function useScrollIntoViewOnHash(id: string, ready: boolean) {
  const { hash } = useLocation()

  useEffect(() => {
    if (!ready || hash !== `#${id}`) return
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [hash, id, ready])
}
