import { useCallback, useEffect, useMemo, useState } from 'react'
import { CircleAlert, Info, RotateCcw } from 'lucide-react'
import { useBlocker } from 'react-router-dom'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { isPermissionError } from '@/lib/api-error'

import {
  configurationErrors,
  draftFromConfiguration,
  emptyGlobalConfigurationDraft,
  isDirty,
  toUpdateRequest,
  useGlobalConfiguration,
  useGlobalConfigurationMutations,
  type GlobalConfigurationDraft,
} from './queries'

/**
 * The global configuration: what gets published to every gateway.
 *
 * One document, so the whole page is one form and one save. A partial save
 * would leave a published file that mixes two edits, and a snapshot is immutable
 * once published — there is no way to take one back apart.
 */
export function GlobalConfigurationPage() {
  const { data, isPending, isError, error, refetch } = useGlobalConfiguration()
  const { update } = useGlobalConfigurationMutations()

  // The loaded values are the baseline a reset returns to, and what dirty is
  // measured against. Kept separately from the draft so a reset can restore it.
  const [baseline, setBaseline] = useState<GlobalConfigurationDraft | null>(null)
  const [draft, setDraft] = useState<GlobalConfigurationDraft>(emptyGlobalConfigurationDraft)
  const [savedAt, setSavedAt] = useState<string | null>(null)

  // A refetch that returns different values should become the new baseline, or
  // the page would show a form that no longer matches what it loaded.
  useEffect(() => {
    if (!data) return
    const next = draftFromConfiguration(data)
    setBaseline(next)
    setDraft(next)
  }, [data])

  const errors = useMemo(() => configurationErrors(draft), [draft])
  const hasErrors = Object.keys(errors).length > 0
  const dirty = baseline !== null && isDirty(draft, baseline)

  const save = useCallback(() => {
    if (hasErrors) return
    update.mutate(toUpdateRequest(draft), {
      onSuccess: (response) => {
        const next = draftFromConfiguration(response)
        setBaseline(next)
        setDraft(next)
        setSavedAt(response.updatedAt)
      },
    })
  }, [draft, hasErrors, update])

  const guard = useUnsavedChangesGuard(dirty)

  if (isPending) return <ConfigurationSkeleton />
  if (isError || !data) {
    return (
      <Alert variant="destructive">
        <CircleAlert aria-hidden="true" className="size-4" />
        <AlertTitle>Could not load the global configuration</AlertTitle>
        <AlertDescription>
          {error instanceof Error ? error.message : String(error ?? 'Nothing was returned')}
          <Button
            variant="outline"
            size="sm"
            className="ml-3"
            onClick={() => {
              refetch()
            }}
          >
            Try again
          </Button>
        </AlertDescription>
      </Alert>
    )
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold">Global Configuration</h1>
        <p className="text-sm text-muted-foreground">
          Published to every gateway as part of each snapshot. A change here does not
          take effect until a new snapshot is published.
        </p>
      </header>

      <Alert>
        <Info aria-hidden="true" className="size-4" />
        <AlertTitle>This is one document</AlertTitle>
        <AlertDescription>
          Saving writes all of it at once. A snapshot is immutable once published, so a
          partial save would produce a file that mixes two edits.
        </AlertDescription>
      </Alert>

      <form
        onSubmit={(event) => {
          event.preventDefault()
          save()
        }}
        className="space-y-6"
      >
        <CoreSection draft={draft} errors={errors} onChange={setDraft} />
        <RateLimitSection draft={draft} errors={errors} onChange={setDraft} />
        <QoSSection draft={draft} errors={errors} onChange={setDraft} />
        <HttpHandlerSection draft={draft} errors={errors} onChange={setDraft} />
        <ServiceDiscoverySection draft={draft} errors={errors} onChange={setDraft} />

        {update.error ? <SaveError error={update.error} /> : null}

        {guard.blocked ? (
          <UnsavedChangesDialog
            onSaveAndLeave={() => {
              if (!hasErrors) save()
              guard.proceed()
            }}
            onLeave={guard.proceed}
            onStay={guard.cancel}
          />
        ) : null}

        <ActionBar
          dirty={dirty}
          isSaving={update.isPending}
          hasErrors={hasErrors}
          savedAt={savedAt ?? data.updatedAt}
          onReset={() => {
            if (baseline) setDraft(baseline)
          }}
        />
      </form>
    </div>
  )
}

/**
 * Warns before a draft with unsaved changes is lost.
 *
 * Two different kinds of leaving, because the browser only lets us intercept one
 * of them: `beforeunload` covers closing the tab or reloading, and the router's
 * blocker covers navigating to another page. Missing either leaves a real way to
 * lose the edit.
 */
function useUnsavedChangesGuard(
  dirty: boolean,
): { blocked: boolean; proceed: () => void; cancel: () => void } {
  useEffect(() => {
    if (!dirty) return

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      // A bare preventDefault is ignored by modern browsers; the returnValue is
      // what still triggers the native prompt.
      event.returnValue = ''
    }

    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [dirty])

  const blocker = useBlocker(
    // Saving leaves the same page, so the blocker would otherwise fire on it.
    ({ currentLocation, nextLocation }) =>
      dirty && currentLocation.pathname !== nextLocation.pathname,
  )

  const proceed = useCallback(() => {
    if (blocker.state === 'blocked') blocker.proceed()
  }, [blocker])

  const cancel = useCallback(() => {
    if (blocker.state === 'blocked') blocker.reset()
  }, [blocker])

  return { blocked: blocker.state === 'blocked', proceed, cancel }
}

/** The in-app half of the guard, rendered by the page that owns the draft. */
export function UnsavedChangesDialog({
  onSaveAndLeave,
  onLeave,
  onStay,
}: {
  onSaveAndLeave: () => void
  onLeave: () => void
  onStay: () => void
}) {
  return (
    <AlertDialog open>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Unsaved changes</AlertDialogTitle>
          <AlertDialogDescription>
            This configuration has changes that have not been saved.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={onStay}>Stay here</AlertDialogCancel>
          <Button variant="outline" onClick={onLeave}>
            Leave without saving
          </Button>
          <AlertDialogAction onClick={onSaveAndLeave}>Save and leave</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

function CoreSection({
  draft,
  errors,
  onChange,
}: {
  draft: GlobalConfigurationDraft
  errors: Record<string, string>
  onChange: (draft: GlobalConfigurationDraft) => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Core</CardTitle>
        <CardDescription>Applied to every published request.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4 sm:grid-cols-2">
        <Field
          id="gc-base-url"
          label="Base URL"
          value={draft.baseUrl}
          error={errors.baseUrl}
          hint="The address requests are forwarded from."
          onChange={(baseUrl) => onChange({ ...draft, baseUrl })}
        />
        <Field
          id="gc-downstream-scheme"
          label="Downstream scheme"
          value={draft.downstreamScheme}
          error={errors.downstreamScheme}
          hint="http or https. Must agree with the base URL."
          onChange={(downstreamScheme) => onChange({ ...draft, downstreamScheme })}
        />
        <Field
          id="gc-request-id-key"
          label="Request ID header"
          value={draft.requestIdKey}
          hint="The header a request id is read from and written to."
          onChange={(requestIdKey) => onChange({ ...draft, requestIdKey })}
        />
        <Field
          id="gc-timeout"
          label="Timeout (ms)"
          value={draft.timeout}
          error={errors.timeout}
          type="number"
          hint="Blank leaves it unset."
          onChange={(timeout) => onChange({ ...draft, timeout })}
        />
      </CardContent>
    </Card>
  )
}

function RateLimitSection(props: SectionProps) {
  const { draft, errors, onChange } = props
  return (
    <Card>
      <CardHeader>
        <CardTitle>Rate limiting</CardTitle>
        <CardDescription>What a throttled request is answered with.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4 sm:grid-cols-2">
        <ToggleRow
          id="gc-rate-limit-enabled"
          label="Enable rate limiting"
          checked={draft.rateLimit.enableRateLimiting}
          onChange={(enableRateLimiting) =>
            onChange({ ...draft, rateLimit: { ...draft.rateLimit, enableRateLimiting } })
          }
        />
        <Field
          id="gc-rate-limit-status"
          label="HTTP status code"
          value={draft.rateLimit.httpStatusCode}
          error={errors['rateLimit.httpStatusCode']}
          hint="Three digits, e.g. 429."
          onChange={(httpStatusCode) =>
            onChange({ ...draft, rateLimit: { ...draft.rateLimit, httpStatusCode } })
          }
        />
      </CardContent>
    </Card>
  )
}

function QoSSection(props: SectionProps) {
  const { draft, errors, onChange } = props
  return (
    <Card>
      <CardHeader>
        <CardTitle>QoS</CardTitle>
        <CardDescription>How long a downstream is given before the circuit opens.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4 sm:grid-cols-2">
        <Field
          id="gc-qos-timeout"
          label="Timeout value (ms)"
          value={draft.qoS.timeoutValue}
          error={errors['qoS.timeoutValue']}
          type="number"
          onChange={(timeoutValue) =>
            onChange({ ...draft, qoS: { ...draft.qoS, timeoutValue } })
          }
        />
        <Field
          id="gc-qos-break"
          label="Duration of break (ms)"
          value={draft.qoS.durationOfBreak}
          error={errors['qoS.durationOfBreak']}
          type="number"
          onChange={(durationOfBreak) =>
            onChange({ ...draft, qoS: { ...draft.qoS, durationOfBreak } })
          }
        />
      </CardContent>
    </Card>
  )
}

function HttpHandlerSection(props: SectionProps) {
  const { draft, errors, onChange } = props
  return (
    <Card>
      <CardHeader>
        <CardTitle>HTTP handler</CardTitle>
        <CardDescription>How the gateway's client behaves downstream.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4 sm:grid-cols-2">
        <ToggleRow
          id="gc-use-proxy"
          label="Use a proxy"
          checked={draft.httpHandler.useProxy}
          onChange={(useProxy) =>
            onChange({ ...draft, httpHandler: { ...draft.httpHandler, useProxy } })
          }
        />
        <ToggleRow
          id="gc-expect-continue"
          label="Expect 100-continue"
          checked={draft.httpHandler.expect100Continue}
          onChange={(expect100Continue) =>
            onChange({ ...draft, httpHandler: { ...draft.httpHandler, expect100Continue } })
          }
        />
        <Field
          id="gc-max-connections"
          label="Max connections per server"
          value={draft.httpHandler.maxConnectionsPerServer}
          error={errors['httpHandler.maxConnectionsPerServer']}
          type="number"
          hint="Blank leaves the framework default."
          onChange={(maxConnectionsPerServer) =>
            onChange({ ...draft, httpHandler: { ...draft.httpHandler, maxConnectionsPerServer } })
          }
        />
      </CardContent>
    </Card>
  )
}

function ServiceDiscoverySection(props: SectionProps) {
  const { draft, errors, onChange } = props
  const discovery = draft.serviceDiscovery

  return (
    <Card>
      <CardHeader>
        <CardTitle>Service discovery</CardTitle>
        <CardDescription>
          Optional. Everything here is ignored unless a provider is named.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field
            id="gc-provider"
            label="Provider"
            value={discovery.provider}
            hint="For example Consul, Eureka or None."
            onChange={(provider) =>
              onChange({ ...draft, serviceDiscovery: { ...discovery, provider } })
            }
          />
          <Field
            id="gc-discovery-type"
            label="Type"
            value={discovery.type}
            onChange={(type) => onChange({ ...draft, serviceDiscovery: { ...discovery, type } })}
          />
          <Field
            id="gc-discovery-host"
            label="Host"
            value={discovery.host}
            onChange={(host) => onChange({ ...draft, serviceDiscovery: { ...discovery, host } })}
          />
          <Field
            id="gc-discovery-port"
            label="Port"
            value={discovery.port}
            error={errors['serviceDiscovery.port']}
            type="number"
            onChange={(port) => onChange({ ...draft, serviceDiscovery: { ...discovery, port } })}
          />
        </div>

        {errors.serviceDiscovery ? <FieldError>{errors.serviceDiscovery}</FieldError> : null}

        <div className="space-y-1.5">
          <Label htmlFor="gc-discovery-config">Provider settings</Label>
          <textarea
            id="gc-discovery-config"
            rows={4}
            placeholder={'PollingInterval: 5000\nNamespace: Production'}
            className="w-full rounded-md border border-input bg-transparent px-3 py-2 font-mono text-sm"
            value={discovery.configuration}
            aria-invalid={Boolean(errors['serviceDiscovery.configuration'])}
            onChange={(event) =>
              onChange({
                ...draft,
                serviceDiscovery: { ...discovery, configuration: event.target.value },
              })
            }
          />
          <p className="text-xs text-muted-foreground">
            One <code className="font-mono">key: value</code> per line, passed to the
            provider verbatim. Only the first colon separates, so a value may contain
            more.
          </p>
          {errors['serviceDiscovery.configuration'] ? (
            <FieldError>{errors['serviceDiscovery.configuration']}</FieldError>
          ) : null}
        </div>
      </CardContent>
    </Card>
  )
}

interface SectionProps {
  draft: GlobalConfigurationDraft
  errors: Record<string, string>
  onChange: (draft: GlobalConfigurationDraft) => void
}

function ActionBar({
  dirty,
  isSaving,
  hasErrors,
  savedAt,
  onReset,
}: {
  dirty: boolean
  isSaving: boolean
  hasErrors: boolean
  savedAt: string
  onReset: () => void
}) {
  return (
    <div className="sticky bottom-0 flex flex-wrap items-center gap-3 border-t bg-background/95 py-3 backdrop-blur">
      <Button type="submit" disabled={!dirty || isSaving || hasErrors}>
        {isSaving ? 'Saving…' : 'Save'}
      </Button>
      <Button
        type="button"
        variant="outline"
        disabled={!dirty || isSaving}
        onClick={onReset}
      >
        <RotateCcw aria-hidden="true" className="mr-1 size-3.5" />
        Reset
      </Button>
      <Separator orientation="vertical" className="h-5" />
      <span className="text-xs text-muted-foreground">
        {dirty ? 'Unsaved changes' : `Last updated ${formatTimestamp(savedAt)}`}
      </span>
    </div>
  )
}

function Field({
  id,
  label,
  value,
  onChange,
  error,
  hint,
  type = 'text',
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
  hint?: string
  type?: string
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        value={value}
        aria-invalid={Boolean(error)}
        onChange={(event) => onChange(event.target.value)}
      />
      {hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
      {error ? <FieldError>{error}</FieldError> : null}
    </div>
  )
}

function ToggleRow({
  id,
  label,
  checked,
  onChange,
}: {
  id: string
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <div className="flex items-center gap-2 pt-1.5">
      <Checkbox id={id} checked={checked} onCheckedChange={(value) => onChange(value === true)} />
      <Label htmlFor={id}>{label}</Label>
    </div>
  )
}

function FieldError({ children }: { children: React.ReactNode }) {
  return (
    <p className="flex items-center gap-1 text-xs text-destructive">
      <CircleAlert className="size-3" aria-hidden="true" />
      {children}
    </p>
  )
}

function SaveError({ error }: { error: unknown }) {
  const message = error instanceof Error ? error.message : String(error)
  const permission = isPermissionError(error)

  return (
    <Alert variant="destructive">
      <CircleAlert aria-hidden="true" className="size-4" />
      <AlertTitle>{permission ? 'Not permitted' : 'The configuration was refused'}</AlertTitle>
      <AlertDescription>
        {permission
          ? 'Changing the global configuration needs the Admin role.'
          : message}
      </AlertDescription>
    </Alert>
  )
}

function ConfigurationSkeleton() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-8 w-56" />
      <Skeleton className="h-14 w-full" />
      <Skeleton className="h-56 w-full" />
      <Skeleton className="h-40 w-full" />
    </div>
  )
}

function formatTimestamp(value: string): string {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}
