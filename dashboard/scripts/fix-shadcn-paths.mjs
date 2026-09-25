/**
 * Works around the shadcn CLI resolving the `@/` path alias literally.
 *
 * `npx shadcn@latest add <component>` writes to `./@/components/...` instead of
 * `./src/components/...` in this repo, because the alias in tsconfig is not
 * applied to its filesystem writes. Run this after every `shadcn add`:
 *
 *   npx shadcn@latest add table && npm run fix:shadcn-paths
 */
import { cpSync, existsSync, rmSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const projectRoot = dirname(dirname(fileURLToPath(import.meta.url)))
const stray = join(projectRoot, '@')

if (!existsSync(stray)) {
  console.log('No stray @/ directory — nothing to move.')
  process.exit(0)
}

for (const dir of ['components', 'lib', 'hooks']) {
  const from = join(stray, dir)
  if (!existsSync(from)) continue

  const to = join(projectRoot, 'src', dir)
  cpSync(from, to, { recursive: true })
  console.log(`Moved @/${dir} -> src/${dir}`)
}

rmSync(stray, { recursive: true, force: true })
console.log('Removed stray @/ directory.')
