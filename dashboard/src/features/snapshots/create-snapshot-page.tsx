import { useState } from 'react'
import { ArrowRight, Check, X } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { isPermissionError } from '@/lib/api-error'
import type { SnapshotResponse } from '@/api'

import {
  useCreateSourceCounts,
  useCreateSnapshot,
  useLatestPublishedSnapshot,
} from './create-model'
import { OCELOT_VERSIONS } from './queries'

/**
 * Creating a snapshot, as a decision rather than a button.
 *
 * The create endpoint takes the current management state and nothing else, so a
 * one-click button would capture whatever happened to be there. This page exists
 * to make that state visible first: what will be included, what it will be
 * compared against, and — plainly — that creating does not publish it anywhere.
 */

/** The two sources the API can actually snapshot. */
type Source = 'current' | 'baseline'

export function CreateSnapshotPage() {
  const navigate = useNavigate()
  const [source, setSource] = useState<Source>('current')
  const [label, setLabel] = useState('')
  const [summary, setSummary] = useState('')
  const [version, setVersion] = useState(OCELOT_VERSIONS[0])

  const { counts, isPending: counting } = useCreateSourceCounts()
  const published = useLatestPublishedSnapshot()
  const { create, isPending, error, data: created } = useCreateSnapshot()

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold">Create configuration snapshot</h1>
          <p className="text-sm text-muted-foreground">
            Build an immutable runtime artifact from the current management state.
            Creating a snapshot does not publish it to gateways.
          </p>
        </div>
        <Button variant="ghost" onClick={() => navigate('/snapshots')}>
          <X aria-hidden="true" className="size-4" />
          Cancel
        </Button>
      </header>

      <Workflow />

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>Snapshot source</CardTitle>
              <CardDescription>
                Choose the configuration state that will be sealed into this snapshot.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <SourceOption
                selected={source === 'current'}
                onSelect={() => setSource('current')}
                title="Current management state"
                badge="Recommended"
                description="Resolve the latest saved Routes, Services, Global Configuration, and enabled plugin configuration."
                counts={counts}
                counting={counting}
              />

              <SourceOption
                selected={source === 'baseline'}
                onSelect={() => setSource('baseline')}
                title={
                  published.snapshot
                    ? `Use snapshot #${published.snapshot.version} as comparison baseline`
                    : 'Use a published snapshot as comparison baseline'
                }
                disabled={!published.snapshot}
                description={
                  published.snapshot
                    ? `Retain a clear comparison to the currently published desired state #${published.snapshot.version} before validation.`
                    : 'No snapshot has been published yet, so there is no baseline to compare against.'
                }
              />

              {source === 'baseline' && published.snapshot ? (
                <div className="rounded-md border bg-muted/30 p-3 text-xs">
                  <p className="text-muted-foreground">
                    The artifact is still built from the current management state — the API has
                    one source. Choosing a baseline records what this snapshot will be judged
                    against, so a later review has something to compare it to.
                  </p>
                </div>
              ) : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Release context</CardTitle>
              <CardDescription>
                Add traceable operator context for audit and downstream publication decisions.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="snapshot-label">Snapshot label (optional)</Label>
                  <Input
                    id="snapshot-label"
                    value={label}
                    placeholder="e.g. September payments policy update"
                    onChange={(event) => setLabel(event.target.value)}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="ocelot-version">Ocelot target version</Label>
                  <Select value={version} onValueChange={(value) => setVersion(value as typeof version)}>
                    <SelectTrigger id="ocelot-version" className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {OCELOT_VERSIONS.map((value) => (
                        <SelectItem key={value} value={value}>
                          Ocelot {value}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    The version is chosen once at setup and is the same for every snapshot.
                    Changing it here would not change the generated artifact.
                  </p>
                </div>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="change-summary">Change summary (optional)</Label>
                <Textarea
                  id="change-summary"
                  value={summary}
                  rows={3}
                  placeholder="Describe why this configuration snapshot is being created for audit and publication review."
                  onChange={(event) => setSummary(event.target.value)}
                />
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="space-y-4">
          <LifecycleProtection />
          <CurrentPublishedState snapshot={published.snapshot} loading={published.isPending} />

          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Next</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-xs text-muted-foreground">
                Resolve the effective Ocelot configuration and run validation.
              </p>

              {error ? (
                <Alert variant="destructive">
                  <AlertTitle>
                    {isPermissionError(error) ? 'Not permitted' : 'The snapshot was not created'}
                  </AlertTitle>
                  <AlertDescription>
                    {isPermissionError(error)
                      ? 'Creating a snapshot needs the SnapshotManager or Admin role.'
                      : error instanceof Error
                        ? error.message
                        : String(error)}
                  </AlertDescription>
                </Alert>
              ) : null}

              {created ? (
                <Alert>
                  <AlertTitle>Snapshot #{created.version} created</AlertTitle>
                  <AlertDescription>
                    It is not running anywhere until it is published.{' '}
                    <code className="font-mono text-xs">{created.hash.slice(0, 12)}…</code>
                    <Button
                      variant="link"
                      size="sm"
                      className="ml-2 h-auto p-0"
                      onClick={() => navigate('/snapshots')}
                    >
                      Go to snapshots
                    </Button>
                  </AlertDescription>
                </Alert>
              ) : null}

              <Button
                className="w-full"
                disabled={isPending || counting}
                onClick={() =>
                  create.mutate({
                    // The label and summary are not accepted by the API yet; they
                    // are shown here so the operator records them, and saying so
                    // beats silently dropping what they typed.
                    initiatedBy: CURRENT_OPERATOR,
                    correlationId: label.trim() || undefined,
                  })
                }
              >
                {isPending ? 'Creating…' : 'Create snapshot'}
                <ArrowRight aria-hidden="true" className="size-4" />
              </Button>

              {label.trim() || summary.trim() ? (
                <p className="text-xs text-muted-foreground">
                  The API does not yet store the label or change summary, so this snapshot will
                  not carry them. They stay on screen until it does.
                </p>
              ) : null}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}

/**
 * Who the snapshot is attributed to.
 *
 * Fixed until the dashboard resolves a real session (#433). Sent rather than
 * left blank because the API requires it and the audit record is the point.
 */
const CURRENT_OPERATOR = 'dashboard'

/** The three stages a snapshot goes through, with the one in progress marked. */
function Workflow() {
  const steps = [
    { n: 1, title: 'Select source', detail: 'Management state' },
    { n: 2, title: 'Validate', detail: 'Resolve configuration' },
    { n: 3, title: 'Review & create', detail: 'Seal immutable artifact' },
  ]

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-sm">Snapshot workflow</CardTitle>
            <CardDescription>
              The resulting artifact is versioned, hashed, validated, and immutable.
            </CardDescription>
          </div>
          <span className="text-xs text-muted-foreground">Step 1 of 3</span>
        </div>
      </CardHeader>
      <CardContent>
        <ol className="grid gap-2 sm:grid-cols-3">
          {steps.map((step) => (
            <li
              key={step.n}
              className={
                step.n === 1
                  ? 'rounded-md border border-primary bg-primary/5 p-3'
                  : 'rounded-md border p-3'
              }
            >
              <p className="flex items-center gap-2 text-sm font-medium">
                <span
                  className={
                    step.n === 1
                      ? 'flex size-5 items-center justify-center rounded-full bg-primary text-xs text-primary-foreground'
                      : 'flex size-5 items-center justify-center rounded-full border text-xs text-muted-foreground'
                  }
                >
                  {step.n}
                </span>
                {step.title}
              </p>
              <p className="mt-1 pl-7 text-xs text-muted-foreground">{step.detail}</p>
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  )
}

function SourceOption({
  selected,
  onSelect,
  title,
  description,
  badge,
  counts,
  counting,
  disabled,
}: {
  selected: boolean
  onSelect: () => void
  title: string
  description: string
  badge?: string
  counts?: { routes: number | null; services: number | null }
  counting?: boolean
  disabled?: boolean
}) {
  return (
    <label
      className={
        disabled
          ? 'block cursor-not-allowed rounded-md border p-3 opacity-60'
          : 'block cursor-pointer rounded-md border p-3 hover:bg-accent/40'
      }
    >
      <span className="flex items-start gap-3">
        <input
          type="radio"
          name="snapshot-source"
          checked={selected}
          disabled={disabled}
          onChange={onSelect}
          className="mt-1"
          aria-label={title}
        />
        <span className="flex-1 space-y-1">
          <span className="flex items-center gap-2 text-sm font-medium">
            {title}
            {badge ? (
              <span className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
                {badge}
              </span>
            ) : null}
          </span>
          <span className="block text-xs text-muted-foreground">{description}</span>
          {counts ? (
            <span className="mt-2 flex flex-wrap gap-2">
              {counting ? (
                <Skeleton className="h-5 w-40" />
              ) : (
                <>
                  <CountChip label="routes" value={counts.routes} />
                  <CountChip label="services" value={counts.services} />
                  <span className="rounded border px-1.5 py-0.5 text-xs text-muted-foreground">
                    Global configuration resolved
                  </span>
                </>
              )}
            </span>
          ) : null}
        </span>
      </span>
    </label>
  )
}

/** A count, with an honest blank when the API could not supply it. */
function CountChip({ label, value }: { label: string; value: number | null }) {
  return (
    <span className="rounded border px-1.5 py-0.5 text-xs text-muted-foreground">
      {value === null ? `— ${label}` : `${value} ${label}`}
    </span>
  )
}

function LifecycleProtection() {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm">Lifecycle protection</CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {[
          'Immutable by design — the configuration cannot be edited after creation.',
          'Validation required — schema, conflict, plugin, and compatibility checks run before creation.',
          'Publication is separate — new snapshots do not change the running gateways.',
        ].map((line) => (
          <p key={line} className="flex items-start gap-2 text-xs text-muted-foreground">
            <Check aria-hidden="true" className="mt-0.5 size-3.5 shrink-0 text-primary" />
            <span>{line}</span>
          </p>
        ))}
      </CardContent>
    </Card>
  )
}

function CurrentPublishedState({
  snapshot,
  loading,
}: {
  snapshot: SnapshotResponse | null | undefined
  loading: boolean
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm">Current published state</CardTitle>
      </CardHeader>
      <CardContent className="space-y-2 text-xs">
        {loading ? (
          <Skeleton className="h-12 w-full" />
        ) : snapshot ? (
          <>
            <p className="flex items-center justify-between">
              <span className="text-muted-foreground">Desired snapshot</span>
              <span className="font-medium">#{snapshot.version}</span>
            </p>
            <p className="text-muted-foreground">
              Gateway reconciliation is tracked per publication, not per snapshot. Publish the new
              artifact to see how many gateways applied it.
            </p>
          </>
        ) : (
          <p className="text-muted-foreground">
            No snapshot has been published yet. The first one you create becomes the desired
            state once it is published.
          </p>
        )}
      </CardContent>
    </Card>
  )
}
