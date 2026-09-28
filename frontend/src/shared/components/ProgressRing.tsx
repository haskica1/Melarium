import clsx from 'clsx'

export interface RingSegment {
  value: number
  /** Tailwind stroke class, e.g. `stroke-emerald-500`. */
  strokeClassName: string
}

/**
 * A round chart with its number in the middle (SPEC-29 dashboard): one segment is a progress ring
 * ("4/8"), several make a donut ("34/42 u roku"). Plain SVG — the value is always written in the
 * centre, so the ring never has to be read by its angle alone.
 */
export default function ProgressRing({
  segments,
  total,
  center,
  sub,
  label,
  size = 88,
  thickness = 9,
}: {
  segments: RingSegment[]
  total: number
  center: string
  sub?: string
  /** Screen-reader description of what the ring shows. */
  label: string
  size?: number
  thickness?: number
}) {
  const r = (size - thickness) / 2
  const circumference = 2 * Math.PI * r
  const single = segments.length === 1
  // A hairline of surface between touching segments, so neighbours read as separate.
  const gap = single ? 0 : 2

  let offset = 0
  const arcs = total > 0
    ? segments
        .filter(s => s.value > 0)
        .map(s => {
          const length = (Math.min(s.value, total) / total) * circumference
          const arc = { ...s, length: Math.max(length - gap, 0), offset }
          offset += length
          return arc
        })
    : []

  return (
    <div className="relative shrink-0" style={{ width: size, height: size }} role="img" aria-label={label}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} className="-rotate-90">
        <circle
          cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={thickness}
          className="stroke-honey-100 dark:stroke-slate-800"
        />
        {arcs.map((arc, i) => (
          <circle
            key={i}
            cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={thickness}
            strokeDasharray={`${arc.length} ${circumference}`}
            strokeDashoffset={-arc.offset}
            strokeLinecap={single ? 'round' : 'butt'}
            className={clsx(arc.strokeClassName, 'transition-[stroke-dasharray] duration-700 ease-out')}
          />
        ))}
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center text-center leading-none">
        <span className={clsx('font-display font-bold text-gray-900 dark:text-slate-50', size >= 88 ? 'text-lg' : 'text-sm')}>
          {center}
        </span>
        {sub && <span className="mt-1 text-[10px] text-gray-500 dark:text-slate-400">{sub}</span>}
      </div>
    </div>
  )
}
