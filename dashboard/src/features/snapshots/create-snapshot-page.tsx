import { useState } from 'react'
import { ArrowRight, Check, FlaskConical, Layers, X } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

import { StatusBadge } from '@/components/status-badge'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { isPermissionError } from '@/lib/api-error'
import type { PreviewSnapshotResponse, SnapshotResponse } from '@/api'

import {
  useCreateSourceCounts,
  useLatestPublishedSnapshot,
  usePreviewSnapshot,
  useSnapshotCreate,
} from './create-model'
import { shortHash } from './queries'

/**
 * Creating a snapshot in the three steps the work actually takes.
 *
 * The API resolves and validates an artifact without storing it, so each step
 * here corresponds to a real transition rather than a decorative one:
 *
 * 1. **Select source** — what will be captured, and what it will be compared to.
 * 2. **Validate** — the artifact is built and every rule is run against it.
 * 3. **Review & create** — the resolved artifact, its hash, and the seal.
 *
 * Step 2 is a server round trip rather than a guess. What it reports is what
 * `POST /api/v1/snapshots` will re-check when it seals, so a preview that passed
 * and a create that failed means the management state moved in between.
 */

/** The three stages, in the order the page walks them. */
const STEPS = [
  { n: 1, title: 'Select source', detail: 'Management state' },
  { n: 2, title: 'Validate', detail: 'Resolve configuration' },
  { n: 3, title: 'Review & create', detail: 'Seal immutable artifact' },
] as const

type Step = 1 | 2 | 3

export function CreateSnapshotPage() {
  const navigate = useNavigate()
  const [step, setStep] = useState<Step>(1)
  const [label, setLabel] = useState('')
  const [summary, setSummary] = useState('')
  const [baseline, setBaseline] = useState<SnapshotResponse | null>(null)

  const preview = usePreviewSnapshot()
  const published = useLatestPublishedSnapshot()
  const source = useCreateSourceCounts()
  const { create, isPending, error, data: created } = useSnapshotCreate()

  const goToValidate = async () => {
    setStep(2)
    // Run the validation now rather than on mount, so arriving at the step is
    // what triggers the work and the operator sees it happen.
    await preview.run()
  }

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

      <Workflow current={step} />

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          {step === 1 ? (
            <Card>
              <CardHeader>
                <CardTitle>Snapshot source</CardTitle>
                <CardDescription>
                  The API builds from the current management state. A baseline only records what this
                  snapshot will be judged against.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <SourceOption
                  selected
                  title="Current management state"
                  badge="Recommended"
                  description="Resolve the latest saved Routes, Services, Global Configuration, and enabled plugin configuration."
                  counts={source.counts}
                />

                <label
                  className={
                    published.snapshot
                      ? 'block cursor-pointer rounded-md border p-3 hover:bg-accent/40'
                      : 'block cursor-not-allowed rounded-md border p-3 opacity-60'
                  }
                >
                  <span className="flex items-start gap-3">
                    <input
                      type="radio"
                      name="snapshot-source"
                      className="mt-1"
                      disabled={!published.snapshot}
                      checked={!!published.snapshot && baseline?.version === published.snapshot.version}
                      onChange={() => setBaseline(published.snapshot ?? null)}
                      aria-label={
                        published.snapshot
                          ? `Use snapshot #${published.snapshot.version} as comparison baseline`
                          : 'Use a published snapshot as comparison baseline'
                      }
                    />
                    <span className="flex-1 space-y-1">
                      <span className="flex items-center gap-2 text-sm font-medium">
                        {published.snapshot
                          ? `Use snapshot #${published.snapshot.version} as comparison baseline`
                          : 'Use a published snapshot as comparison baseline'}
                      </span>
                      <span className="block text-xs text-muted-foreground">
                        {published.snapshot
                          ? 'Retain a clear comparison to the currently published desired state.'
                          : 'No snapshot has been published yet, so there is no baseline.'}
                      </span>
                    </span>
                  </span>
                </label>
              </CardContent>
            </Card>
          ) : null}

          {step === 2 ? (
            <ValidationStep
              preview={preview.data}
              isPending={preview.isFetching}
              failed={preview.isError}
              error={preview.error}
              onRetry={preview.run}
            />
          ) : null}

          {step === 3 && preview.data ? (
            <Card>
              <CardHeader>
                <CardTitle>Release context</CardTitle>
                <CardDescription>
                  Add traceable operator context for audit and downstream publication decisions.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
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
                  <Label htmlFor="change-summary">Change summary (optional)</Label>
                  <Textarea
                    id="change-summary"
                    rows={3}
                    value={summary}
                    placeholder="Describe why this configuration snapshot is being created for audit and publication review."
                    onChange={(event) => setSummary(event.target.value)}
                  />
                </div>
              </CardContent>
            </Card>
          ) : null}
        </div>

        <div className="space-y-4">
          <LifecycleProtection />

          {step === 3 && preview.data ? (
            <SealPanel
              preview={preview.data}
              label={label.trim()}
              summary={summary.trim()}
              created={created ?? null}
              isPending={isPending}
              error={error}
              onCreate={() =>
                create.mutate({
                  initiatedBy: CURRENT_OPERATOR,
                  // Not accepted by the API yet. It is sent so the two can be
                  // connected without changing this page, and the panel says so.
                  correlationId: label.trim() || undefined,
                })
              }
            />
          ) : null}

          {step < 3 ? (
            <Card>
              <CardContent className="space-y-2 pt-6">
                {step === 1 ? (
                  <Button className="w-full" onClick={goToValidate}>
                    Continue to validation
                    <ArrowRight aria-hidden="true" className="size-4" />
                  </Button>
                ) : (
                  <>
                    <Button variant="outline" className="w-full" onClick={goToValidate}>
                      Re-run validation
                    </Button>
                    <Button
                      className="w-full"
                      // Only offered once the rules passed: sealing a failed
                      // artifact would record a broken configuration as the
                      // desired state.
                      disabled={!preview.data?.isValid}
                      onClick={() => setStep(3)}
                    >
                      Continue to review
                      <ArrowRight aria-hidden="true" className="size-4" />
                    </Button>
                  </>
                )}
              </CardContent>
            </Card>
          ) : null}
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

function Workflow({ current }: { current: Step }) {
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
          <span className="text-xs text-muted-foreground">Step {current} of 3</span>
        </div>
      </CardHeader>
      <CardContent>
        <ol className="grid gap-2 sm:grid-cols-3">
          {STEPS.map((step) => {
            const state = step.n < current ? 'done' : step.n === current ? 'current' : 'pending'
            return (
              <li
                key={step.n}
                className={
                  state === 'current'
                    ? 'rounded-md border border-primary bg-primary/5 p-3'
                    : 'rounded-md border p-3'
                }
                aria-current={state === 'current' ? 'step' : undefined}
              >
                <p className="flex items-center gap-2 text-sm font-medium">
                  <span
                    className={
                      state === 'done'
                        ? 'flex size-5 items-center justify-center rounded-full bg-primary text-primary-foreground'
                        : state === 'current'
                          ? 'flex size-5 items-center justify-center rounded-full bg-primary text-xs text-primary-foreground'
                          : 'flex size-5 items-center justify-center rounded-full border text-xs text-muted-foreground'
                    }
                  >
                    {state === 'done' ? (
                      <Check aria-hidden="true" className="size-3" />
                    ) : (
                      step.n
                    )}
                  </span>
                  {step.title}
                </p>
                <p className="mt-1 pl-7 text-xs text-muted-foreground">{step.detail}</p>
              </li>
            )
          })}
        </ol>
      </CardContent>
    </Card>
  )
}

function SourceOption({
  title,
  description,
  badge,
  counts,
}: {
  selected: boolean
  title: string
  description: string
  badge?: string
  counts: { routes: number | null; services: number | null }
}) {
  return (
    <label className="block cursor-pointer rounded-md border border-primary bg-primary/5 p-3">
      <span className="flex items-start gap-3">
        <input type="radio" name="snapshot-source" checked readOnly className="mt-1" aria-label={title} />
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
          <span className="mt-2 flex flex-wrap gap-2">
            <CountChip label="routes" value={counts.routes} />
            <CountChip label="services" value={counts.services} />
            <span className="rounded border px-1.5 py-0.5 text-xs text-muted-foreground">
              Global configuration resolved
            </span>
          </span>
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

/**
 * Step 2: the artifact the API resolved, and every rule run against it.
 */
function ValidationStep({
  preview,
  isPending,
  failed,
  error,
  onRetry,
}: {
  preview: PreviewSnapshotResponse | null
  isPending: boolean
  failed: boolean
  error: unknown
  onRetry: () => void
}) {
  if (isPending) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Resolving the effective configuration</CardTitle>
          <CardDescription>
            Building the artifact and running the validation rules against it.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-2">
          <Skeleton className="h-6 w-full" />
          <Skeleton className="h-6 w-full" />
          <Skeleton className="h-6 w-2/3" />
        </CardContent>
      </Card>
    )
  }

  if (failed || !preview) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Validation could not be run</AlertTitle>
        <AlertDescription>
          {error instanceof Error ? error.message : String(error ?? 'Nothing was returned')}
          <Button variant="outline" size="sm" className="ml-3" onClick={onRetry}>
            Try again
          </Button>
        </AlertDescription>
      </Alert>
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FlaskConical aria-hidden="true" className="size-4" />
          Resolved configuration
        </CardTitle>
        <CardDescription>
          {preview.routeCount} routes · {preview.serviceCount} services · Ocelot {preview.ocelotVersion}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <StatusBadge status={preview.isValid ? 'Passed' : 'Failed'} />
          <code className="font-mono text-xs text-muted-foreground">
            {preview.hash ? shortHash(preview.hash) : 'No hash — nothing was built'}
          </code>
        </div>

        {!preview.content ? (
          <Alert variant="destructive">
            <AlertTitle>Nothing could be built from the current state</AlertTitle>
            <AlertDescription>
              The management state could not be turned into a configuration, so there is no
              artifact to seal.
            </AlertDescription>
          </Alert>
        ) : null}

        <ul className="space-y-1.5 text-xs">
          {preview.validationResults.map((result) => (
            <li key={result.rule} className="flex items-start gap-2">
              <StatusBadge
                status={result.isValid ? 'Passed' : result.message?.startsWith('Warning') ? 'Warning' : 'Failed'}
                className="shrink-0"
              />
              <div>
                <div className="font-medium">{result.rule}</div>
                {result.message ? (
                  <div className="text-muted-foreground">{result.message}</div>
                ) : null}
              </div>
            </li>
          ))}
        </ul>

        {!preview.isValid ? (
          <Alert variant="destructive">
            <AlertTitle>Validation failed</AlertTitle>
            <AlertDescription>
              Fix the failed rules in the management state, then validate again. Sealing an
              artifact that failed would record a broken configuration as the desired state.
            </AlertDescription>
          </Alert>
        ) : null}
      </CardContent>
    </Card>
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

/** Step 3: what is about to be sealed, and the button that seals it. */
function SealPanel({
  preview,
  label,
  summary,
  created,
  isPending,
  error,
  onCreate,
}: {
  preview: PreviewSnapshotResponse
  label: string
  summary: string
  created: SnapshotResponse | null
  isPending: boolean
  error: unknown
  onCreate: () => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-sm">
          <Layers aria-hidden="true" className="size-4" />
          Seal the artifact
        </CardTitle>
        <CardDescription>
          This becomes snapshot #{preview.nextVersion}, immutable and hashed.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
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
              It is not running anywhere until it is published. Hash{' '}
              <code className="font-mono text-xs">{created.hash.slice(0, 12)}…</code>
            </AlertDescription>
          </Alert>
        ) : (
          <>
            <Button className="w-full" disabled={isPending} onClick={onCreate}>
              {isPending ? 'Creating…' : `Create snapshot #${preview.nextVersion}`}
            </Button>

            {label || summary ? (
              <p className="text-xs text-muted-foreground">
                The API does not yet store the label or change summary, so this snapshot will not
                carry them.
              </p>
            ) : null}
          </>
        )}
      </CardContent>
    </Card>
  )
}
