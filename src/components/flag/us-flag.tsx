import { cn } from '@/lib/utils'

/*
 * Geometry per Executive Order 10834 (1959), expressed against a hoist of
 * 1000 units. Everything below is derived, not eyeballed.
 */
const HOIST = 1000 // A — flag height
const FLY = 1900 // B — flag width, 1:1.9
const UNION_HOIST = (7 / 13) * HOIST // C — union spans 7 of the 13 stripes
const UNION_FLY = 0.76 * HOIST // D
const STRIPE = HOIST / 13 // L
const STAR_RADIUS = (0.0616 * HOIST) / 2 // K/2

// A five-pointed star's inner radius, from the ratio of its point angles.
const INNER_RATIO = Math.sin(Math.PI / 10) / Math.sin((3 * Math.PI) / 10)

function starPath(cx: number, cy: number, outer: number): string {
  const inner = outer * INNER_RATIO
  const points: string[] = []
  // 10 alternating vertices, starting at the upward point (-90°).
  for (let i = 0; i < 10; i++) {
    const r = i % 2 === 0 ? outer : inner
    const angle = (Math.PI / 5) * i - Math.PI / 2
    points.push(`${(cx + r * Math.cos(angle)).toFixed(2)},${(cy + r * Math.sin(angle)).toFixed(2)}`)
  }
  return points.join(' ')
}

/*
 * The canonical 50-star arrangement: nine rows alternating six and five stars.
 * Odd rows take the odd twelfths of the union's width, even rows the even
 * twelfths — which interleaves them into the familiar offset grid.
 *   5 rows x 6 stars + 4 rows x 5 stars = 50.
 */
function starCentres(): Array<{ x: number; y: number }> {
  const stars: Array<{ x: number; y: number }> = []
  for (let row = 1; row <= 9; row++) {
    const sixStarRow = row % 2 === 1
    const columns = sixStarRow ? [1, 3, 5, 7, 9, 11] : [2, 4, 6, 8, 10]
    for (const col of columns) {
      stars.push({
        x: (UNION_FLY * col) / 12,
        y: (UNION_HOIST * row) / 10,
      })
    }
  }
  return stars
}

const STARS = starCentres()

export interface UsFlagProps {
  className?: string
  /** Gentle fold/wave motion. Always disabled under prefers-reduced-motion. */
  animated?: boolean
  /** Decorative by default; pass a title to expose it to assistive tech. */
  title?: string
}

export function UsFlag({ className, animated = true, title }: UsFlagProps) {
  return (
    <svg
      viewBox={`0 0 ${FLY} ${HOIST}`}
      className={cn('h-auto w-full select-none', className)}
      role={title ? 'img' : 'presentation'}
      aria-hidden={title ? undefined : true}
      aria-label={title}
    >
      {title ? <title>{title}</title> : null}

      <defs>
        {/* Soft directional shading, drifting across the flag to suggest folds. */}
        <linearGradient id="flag-folds" x1="0" y1="0" x2="1" y2="0.35">
          <stop offset="0%" stopColor="#000" stopOpacity="0.20" />
          <stop offset="18%" stopColor="#fff" stopOpacity="0.14" />
          <stop offset="38%" stopColor="#000" stopOpacity="0.16" />
          <stop offset="58%" stopColor="#fff" stopOpacity="0.10" />
          <stop offset="78%" stopColor="#000" stopOpacity="0.18" />
          <stop offset="100%" stopColor="#fff" stopOpacity="0.08" />
        </linearGradient>

        <clipPath id="flag-clip">
          <rect x="0" y="0" width={FLY} height={HOIST} rx="4" />
        </clipPath>
      </defs>

      <g clipPath="url(#flag-clip)" className={cn(animated && 'flag-wave')}>
        {/* 13 stripes, red first and last. */}
        <rect x="0" y="0" width={FLY} height={HOIST} fill="var(--flag-white)" />
        {Array.from({ length: 13 }, (_, i) =>
          i % 2 === 0 ? (
            <rect
              key={i}
              x="0"
              y={i * STRIPE}
              width={FLY}
              height={STRIPE}
              fill="var(--flag-red)"
            />
          ) : null,
        )}

        {/* Canton. */}
        <rect x="0" y="0" width={UNION_FLY} height={UNION_HOIST} fill="var(--flag-blue)" />

        {STARS.map((s, i) => (
          <polygon
            key={i}
            points={starPath(s.x, s.y, STAR_RADIUS)}
            fill="var(--flag-white)"
          />
        ))}

        {/* Shading sits above everything so folds read across stripes and canton alike. */}
        <rect
          x="0"
          y="0"
          width={FLY}
          height={HOIST}
          fill="url(#flag-folds)"
          className={cn('flag-folds', animated && 'flag-folds-animated')}
        />
      </g>
    </svg>
  )
}
