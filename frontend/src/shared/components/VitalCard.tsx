import { useEffect, useRef, useState } from 'react'

// ── Count-up hook (easeOutCubic, respects reduced motion) ───────────────────────

function useCountUp(target: number, decimals: number, durationMs = 900) {
  const [val, setVal] = useState(0)
  const rafRef = useRef<number | undefined>(undefined)

  useEffect(() => {
    const reduce = typeof window !== 'undefined'
      && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    if (reduce || !isFinite(target)) {
      setVal(target)
      return
    }
    const start = performance.now()
    const tick = (now: number) => {
      const t = Math.min(1, (now - start) / durationMs)
      const eased = 1 - Math.pow(1 - t, 3) // easeOutCubic
      setVal(target * eased)
      if (t < 1) rafRef.current = requestAnimationFrame(tick)
      else setVal(target)
    }
    rafRef.current = requestAnimationFrame(tick)
    return () => { if (rafRef.current) cancelAnimationFrame(rafRef.current) }
  }, [target, decimals, durationMs])

  return val.toFixed(decimals)
}

// ── Animated value: counts up the leading number, keeps any unit/suffix ─────────

function AnimatedValue({ value }: { value: string }) {
  const m = value.match(/^(\d[\d,]*(?:\.\d+)?)(.*)$/s)
  const raw = m ? m[1].replace(/,/g, '') : ''
  const decimals = raw.includes('.') ? (raw.split('.')[1]?.length ?? 0) : 0
  const target = m ? parseFloat(raw) : NaN
  const animated = useCountUp(target, decimals)

  if (!m) return <>{value}</>
  return <>{animated}{m[2]}</>
}

// ── Vitals KPI tile (gradient, watermark emoji, count-up value) ──────────────────

export interface VitalCardProps {
  icon: string
  label: string
  value: string
  sub?: string
  subAlert?: boolean
  gradient: string
}

export function VitalCard({ icon, label, value, sub, subAlert, gradient }: VitalCardProps) {
  return (
    <div className={`relative overflow-hidden rounded-2xl p-4 sm:p-5 text-white shadow-lg bg-gradient-to-br ${gradient}`}>
      <span className="absolute -right-2 -top-3 text-6xl opacity-20 select-none pointer-events-none leading-none">
        {icon}
      </span>
      <div className="relative">
        <p className="text-2xl sm:text-3xl font-bold font-display leading-none truncate">
          <AnimatedValue value={value} />
        </p>
        <p className="text-sm font-medium opacity-95 mt-2">{label}</p>
        {sub && (
          <p className={`text-xs mt-0.5 ${subAlert ? 'font-semibold text-white' : 'opacity-80'}`}>
            {subAlert && '⚠ '}{sub}
          </p>
        )}
      </div>
    </div>
  )
}
