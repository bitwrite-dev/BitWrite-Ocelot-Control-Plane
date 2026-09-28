import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Reading the real stylesheet is the point: a copy of the values here would
// happily keep passing after the CSS drifted. `?raw` returns an empty string
// under this runner, so the file is read directly. This test is excluded from
// the app tsconfig — see tsconfig.node.json — because that one has node types
// and this one does not.
const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')

/**
 * The palette's contrast, measured rather than eyeballed.
 *
 * A palette can look fine and still fail, and the failures are invisible until
 * someone with the light on a bright surface tries to read it. These assertions
 * exist so a future tweak cannot quietly drop a pair below its threshold.
 */


type Oklch = { l: number; c: number; h: number }

function toSrgb({ l, c, h }: Oklch): [number, number, number] {
  const rad = (h * Math.PI) / 180
  const a = c * Math.cos(rad)
  const b = c * Math.sin(rad)

  const l_ = l + 0.3963377774 * a + 0.2158037573 * b
  const m_ = l - 0.1055613458 * a - 0.0638541728 * b
  const s_ = l - 0.0894841775 * a - 1.291485548 * b

  const l3 = l_ ** 3
  const m3 = m_ ** 3
  const s3 = s_ ** 3

  const rgb = [
    4.0767416621 * l3 - 3.3077115913 * m3 + 0.2309699292 * s3,
    -1.2684380046 * l3 + 2.6097574011 * m3 - 0.3413193965 * s3,
    -0.0041960863 * l3 - 0.7034186147 * m3 + 1.707614701 * s3,
  ]

  return rgb.map((u) => {
    const clamped = Math.max(0, Math.min(1, u))
    return clamped <= 0.0031308 ? 12.92 * clamped : 1.055 * clamped ** (1 / 2.4) - 0.055
  }) as [number, number, number]
}

function luminance(rgb: [number, number, number]): number {
  const [r, g, b] = rgb.map((c) =>
    c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4,
  )
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function contrast(a: Oklch, b: Oklch): number {
  const la = luminance(toSrgb(a))
  const lb = luminance(toSrgb(b))
  const [hi, lo] = la > lb ? [la, lb] : [lb, la]
  return (hi + 0.05) / (lo + 0.05)
}

/** Reads one variable out of a given block, so light and dark are read apart. */
function variable(block: 'root' | 'dark', name: string): Oklch {
  const start = block === 'root' ? css.indexOf(':root {') : css.indexOf('.dark {')
  const end = css.indexOf('\n}', start)
  const section = css.slice(start, end)

  const match = section.match(new RegExp(`--${name}:\\s*oklch\\(([^)]+)\\)`))
  if (!match) throw new Error(`--${name} is not set in ${block}`)

  const [l, c, h] = match[1].trim().split(/\s+/).map(Number)
  return { l, c, h }
}

describe.each(['root', 'dark'] as const)('the %s palette', (block) => {
  const pairs: ReadonlyArray<[string, string, string, number]> = [
    // The pair an operator reads most: the page and its own text.
    ['foreground', 'background', 'body text', 4.5],
    ['primary-foreground', 'primary', 'label on a primary button', 4.5],
    ['muted-foreground', 'background', 'secondary text', 4.5],
    ['muted-foreground', 'muted', 'text on a muted surface', 4.5],
    ['accent-foreground', 'accent', 'text on an accent surface', 4.5],
    ['destructive', 'background', 'a destructive message', 4.5],
    // Focus ring only has to be visible, not readable.
    ['ring', 'background', 'the focus ring', 3],
  ]

  it.each(pairs)('%s on %s is legible (%s)', (fg, bg, _label, minimum) => {
    expect(contrast(variable(block, fg), variable(block, bg))).toBeGreaterThanOrEqual(
      minimum,
    )
  })
})

describe.each(['root', 'dark'] as const)('the %s chart ramp', (block) => {
  it('separates every adjacent pair', () => {
    // Hue alone is not enough at similar lightness; the first draft produced
    // pairs at 1.1:1, which no chart can be read from.
    for (let i = 1; i < 5; i += 1) {
      const a = variable(block, `chart-${i}`)
      const b = variable(block, `chart-${i + 1}`)
      expect(contrast(a, b), `chart-${i} against chart-${i + 1}`).toBeGreaterThanOrEqual(1.6)
    }
  })

  it('is visible against its own background', () => {
    const bg = variable(block, 'background')
    for (let i = 1; i <= 5; i += 1) {
      expect(
        contrast(variable(block, `chart-${i}`), bg),
        `chart-${i} on the background`,
      ).toBeGreaterThanOrEqual(3)
    }
  })
})

describe('the palette as a whole', () => {
  it('is not grey', () => {
    // The reason this issue exists: every colour had zero chroma.
    expect(variable('root', 'primary').c).toBeGreaterThan(0.1)
    expect(variable('dark', 'primary').c).toBeGreaterThan(0.1)
  })

  it('uses one brand hue throughout', () => {
    // The sidebar used a different hue from everything else, so one shell
    // carried two palettes.
    expect(variable('root', 'sidebar-primary').h).toBeCloseTo(variable('root', 'primary').h, 0)
    expect(variable('dark', 'sidebar-primary').h).toBeCloseTo(variable('dark', 'primary').h, 0)
  })

  it('defines the dark palette exactly once', () => {
    // It was defined twice, and the second one won the cascade — so the new
    // values were silently ignored.
    expect(css.match(/^\.dark \{/gm)?.length).toBe(1)
  })

  it('registers the dark variant the components style against', () => {
    expect(css).toContain('@custom-variant dark')
  })
})
