import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

/**
 * Placeholder shell for #432 (bootstrap).
 *
 * This is deliberately not the real layout — the sidebar, navigation and
 * responsive shell are #434. This only proves the toolchain is wired up:
 * Vite + React + TypeScript, Tailwind v4, shadcn/ui, and the `@/` path alias.
 */
export default function App() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-background p-6">
      <Card className="w-full max-w-lg">
        <CardHeader>
          {/* shadcn's CardTitle renders a div, so the page would otherwise have
              no heading landmark. These props restore the semantics without
              touching the generated styling. Revisit when #434 builds the shell. */}
          <CardTitle role="heading" aria-level={1}>
            BitWrite Ocelot Control Plane
          </CardTitle>
          <CardDescription>
            Dashboard bootstrap is in place (#432). The application shell lands in #434.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <dl className="grid grid-cols-2 gap-y-2 text-sm">
            <dt className="text-muted-foreground">Framework</dt>
            <dd>React + Vite + TypeScript</dd>
            <dt className="text-muted-foreground">Styling</dt>
            <dd>Tailwind CSS v4 + shadcn/ui</dd>
            <dt className="text-muted-foreground">API dev proxy</dt>
            <dd className="font-mono text-xs">/api → localhost:5039</dd>
          </dl>
          <p className="text-sm text-muted-foreground">
            The API client, authentication and layout navigation are tracked in #433 and #434.
          </p>
          <Button disabled>Action buttons arrive with #434</Button>
        </CardContent>
      </Card>
    </main>
  )
}
