import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Check, CircleAlert } from 'lucide-react'

import { PageHeader } from '@/components/app-layout'
import { EmptyState, ErrorState, LoadingState } from '@/components/page-state'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Checkbox } from '@/components/ui/checkbox'
import {
  DOWNSTREAM_HTTP_VERSIONS,
  DOWNSTREAM_HTTP_VERSION_POLICIES,
  HTTP_METHODS,
  loadBalancerOptions,
  periodOptions,
  UNSUPPORTED_STEPS,
  WIZARD_STEPS,
  draftFromRoute,
  emptyRouteDraft,
  emptyTransportDraft,
  furthestReachableStep,
  toCreateRequest,
  type RouteDraft,
  type StepId,
} from './wizard-model'
import { toRouteError, useRoute } from '@/features/routes/queries'
import { ApiError } from '@/api'
import {
  useCreateRoute,
  useUpdateRoute,
  useValidateRouteDraft,
  useWizardServices,
} from './queries'
import { stepForField } from './wizard-model'

/** The names a header block sets, for the review row. */
function summariseRules(block: string): string {
  const names = block
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line !== '')
    .map((line) => line.split(':')[0].trim())
  return names.join(', ')
}

/** The HTTP client switches, typed so the draft cannot drift from the form. */
const clientToggles: ReadonlyArray<{
  field: 'allowAutoRedirect' | 'useCookieContainer' | 'useProxy' | 'useTracing'
  label: string
}> = [
  { field: 'allowAutoRedirect', label: 'Follow redirects' },
  { field: 'useCookieContainer', label: 'Share a cookie container across routes' },
  { field: 'useProxy', label: 'Use the configured proxy' },
  { field: 'useTracing', label: 'Add tracing headers to the request' },
]

function Field({
  label,
  htmlFor,
  error,
  hint,
  children,
}: {
  label: string
  htmlFor: string
  error?: string
  hint?: string
  children: React.ReactNode
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
      {error ? (
        <p className="flex items-center gap-1 text-xs text-destructive">
          <CircleAlert className="size-3" aria-hidden="true" />
          {error}
        </p>
      ) : null}
    </div>
  )
}

/** Creates a new route, or replaces an existing one, through the same steps. */
function RouteWizard({ mode, routeId }: { mode: 'create' | 'edit'; routeId?: string }) {
  const [stepIndex, setStepIndex] = useState(0)
  const [draft, setDraft] = useState<RouteDraft>(emptyRouteDraft)
  const [scopeInput, setScopeInput] = useState('')
  // The field is optional so a hand-built partial draft still type-checks, but
  // a draft held in state always comes from emptyRouteDraft or draftFromRoute,
  // so it is complete.
  const transport = draft.transport ?? emptyTransportDraft()
  const headers = draft.headers ?? { add: '', transform: '' }

  const services = useWizardServices()
  const createRoute = useCreateRoute()
  const updateRoute = useUpdateRoute()
  const validateDraft = useValidateRouteDraft()
  // Server errors are held here so a failure can be shown on the step that owns
  // the field, rather than only on Review.
  const [serverErrors, setServerErrors] = useState<Record<number, string[]>>({})
  const navigate = useNavigate()

  // In edit mode the form starts from what is stored, so the steps need the
  // route before anything is editable.
  const existing = useRoute(mode === 'edit' ? routeId : undefined)
  const isEdit = mode === 'edit'

  useEffect(() => {
    if (existing.data) {
      setDraft(draftFromRoute(existing.data))
      setServerErrors({})
    }
  }, [existing.data])

  const isSaving = createRoute.isPending || updateRoute.isPending
  // The save differs by mode, so the error has to come from the one that ran.
  const saveError = isEdit ? updateRoute.error : createRoute.error

  const step = WIZARD_STEPS[stepIndex]
  const isLastStep = stepIndex === WIZARD_STEPS.length - 1
  const stepErrors = useMemo(() => step.validate(draft), [step, draft])
  const reachable = useMemo(() => furthestReachableStep(draft), [draft])

  const update = <K extends keyof RouteDraft>(key: K, value: RouteDraft[K]) =>
    setDraft((current) => ({ ...current, [key]: value }))

  const canAdvance = stepErrors.length === 0

  if (isEdit && existing.isPending) {
    return (
      <>
        <PageHeader title="Edit route" />
        <LoadingState label="Loading route" />
      </>
    )
  }

  if (isEdit && existing.isError) {
    return (
      <>
        <PageHeader title="Edit route" />
        {isNotFound(existing.error) ? (
          <EmptyState
            title="No such route"
            description="It may have been deleted. Nothing was changed."
            action={
              <Button asChild>
                <Link to="/routes">Back to routes</Link>
              </Button>
            }
          />
        ) : (
          <ErrorState {...toRouteError(existing.error)} />
        )}
      </>
    )
  }

  return (
    <>
      <PageHeader
        title={isEdit ? 'Edit route' : 'Create Route'}
        description={
          isEdit && existing.data
            ? `${existing.data.key} — step ${stepIndex + 1} of ${WIZARD_STEPS.length}, ${step.title}`
            : `Step ${stepIndex + 1} of ${WIZARD_STEPS.length} — ${step.title}`
        }
      />

      <div className="grid gap-6 lg:grid-cols-[220px_1fr]">
        {/* Step rail */}
        <nav aria-label="Wizard steps" className="space-y-1">
          <ol className="space-y-1">
            {WIZARD_STEPS.map((entry, index) => {
              const isCurrent = index === stepIndex
              const isDone = index < stepIndex
              const isReachable = index < reachable
              return (
                <li key={entry.id}>
                  <button
                    type="button"
                    onClick={() => setStepIndex(index)}
                    aria-current={isCurrent ? 'step' : undefined}
                    className={[
                      'flex w-full items-center gap-2 rounded-md px-3 py-2 text-left text-sm transition-colors',
                      isCurrent
                        ? 'bg-primary text-primary-foreground font-medium'
                        : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                      !isReachable && !isCurrent ? 'opacity-50' : '',
                    ].join(' ')}
                  >
                    <span className="flex size-5 shrink-0 items-center justify-center rounded-full border text-xs">
                      {isDone ? (
                        <Check className="size-3" aria-hidden="true" />
                      ) : (
                        index + 1
                      )}
                    </span>
                    {entry.title}
                  </button>
                </li>
              )
            })}
          </ol>

          <Separator className="my-4" />

          <div className="space-y-2 px-3">
            <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Not yet supported
            </p>
            {UNSUPPORTED_STEPS.map((entry) => (
              <p key={entry.title} className="text-xs text-muted-foreground">
                {entry.title}{' '}
                <a
                  href={`https://github.com/bitwrite-dev/BitWrite-Ocelot-Control-Plane/issues/${entry.issue}`}
                  className="underline underline-offset-2"
                >
                  #{entry.issue}
                </a>
              </p>
            ))}
            <p className="text-xs text-muted-foreground">
              The API cannot store these yet, so they are listed rather than offered as
              fields that would be discarded on save.
            </p>
          </div>
        </nav>

        {/* Step body */}
        <div className="space-y-6">
          {services.isPending ? (
            <LoadingState label="Loading services" />
          ) : services.isError ? (
            // Without a service list the wizard cannot continue, so this is fatal
            // rather than a banner — say so instead of showing an empty form.
            <ErrorState
              {...toRouteError(services.error)}
              action={
                <Button variant="outline" onClick={() => void services.refetch()}>
                  Retry
                </Button>
              }
            />
          ) : (
            <>
              <section className="space-y-4 rounded-lg border p-5">
                <div>
                  <h2 className="font-heading text-lg font-semibold">{step.title}</h2>
                  <p className="text-sm text-muted-foreground">{step.description}</p>
                </div>

                {/* Problems the API reported for this step's fields. */}
                {serverErrors[stepIndex]?.length ? (
                  <div className="space-y-1 rounded-md border border-destructive/40 bg-destructive/5 p-3">
                    <p className="text-sm font-medium text-destructive">
                      The API rejected this step:
                    </p>
                    <ul className="space-y-0.5">
                      {serverErrors[stepIndex].map((message) => (
                        <li key={message} className="text-sm text-destructive">
                          {message}
                        </li>
                      ))}
                    </ul>
                  </div>
                ) : null}

                {step.id === 'basic' ? (
                  <>
                    <Field
                      label="Key"
                      htmlFor="route-key"
                      error={stepErrors.find((e) => e.startsWith('Key'))}
                      hint="A stable identifier for this route."
                    >
                      <Input
                        id="route-key"
                        value={draft.key}
                        onChange={(event) => update('key', event.target.value)}
                        placeholder="users-list"
                      />
                    </Field>
                    <Field label="Method" htmlFor="route-method">
                      <select
                        id="route-method"
                        className="h-9 w-full max-w-xs rounded-md border bg-background px-3 text-sm"
                        value={draft.method}
                        onChange={(event) => update('method', event.target.value)}
                      >
                        {HTTP_METHODS.map((method) => (
                          <option key={method} value={method}>
                            {method}
                          </option>
                        ))}
                      </select>
                    </Field>
                  </>
                ) : null}

                {step.id === 'upstream' ? (
                  <>
                    <Field
                      label="Upstream path"
                      htmlFor="route-upstream"
                      error={stepErrors.find((e) => e.startsWith('Upstream'))}
                      hint="Placeholders are supported, e.g. /api/{everything}."
                    >
                      <Input
                        id="route-upstream"
                        value={draft.upstreamPath}
                        onChange={(event) => update('upstreamPath', event.target.value)}
                        placeholder="/api/users/{everything}"
                      />
                    </Field>
                    <Field
                      label="Host"
                      htmlFor="route-host"
                      error={stepErrors.find((e) => e.startsWith('Host'))}
                      hint="Optional. Narrows the route to one hostname."
                    >
                      <Input
                        id="route-host"
                        value={draft.host}
                        onChange={(event) => update('host', event.target.value)}
                        placeholder="api.example.com"
                      />
                    </Field>
                  </>
                ) : null}

                {step.id === 'downstream' ? (
                  <>
                    <Field
                      label="Service"
                      htmlFor="route-service"
                      error={stepErrors.find((e) => e.startsWith('A service'))}
                    >
                      <select
                        id="route-service"
                        className="h-9 w-full max-w-sm rounded-md border bg-background px-3 text-sm"
                        value={draft.serviceId}
                        onChange={(event) => update('serviceId', event.target.value)}
                      >
                        <option value="">Select a service…</option>
                        {(services.data?.services ?? []).map((service) => (
                          <option key={service.id} value={service.id}>
                            {service.name}
                          </option>
                        ))}
                      </select>
                    </Field>

                    <div className="space-y-3">
                      <div className="flex items-center justify-between">
                        <Label>Downstream targets</Label>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={draft.downstreamTargets.length >= 10}
                          onClick={() =>
                            update('downstreamTargets', [
                              ...draft.downstreamTargets,
                              { host: '', port: '', scheme: 'http', path: '/' },
                            ])
                          }
                        >
                          Add target
                        </Button>
                      </div>

                      {draft.downstreamTargets.map((target, index) => {
                        const errors = stepErrors.filter((error) =>
                          error.startsWith(`Target ${index + 1}`),
                        )
                        return (
                          <div key={index} className="grid gap-2 sm:grid-cols-[2fr_1fr_1fr_1fr_auto]">
                            <Field
                              label={`Host ${index + 1}`}
                              htmlFor={`target-host-${index}`}
                              error={errors.find((e) => e.includes('host'))}
                            >
                              <Input
                                id={`target-host-${index}`}
                                value={target.host}
                                onChange={(event) =>
                                  update(
                                    'downstreamTargets',
                                    draft.downstreamTargets.map((item, i) =>
                                      i === index ? { ...item, host: event.target.value } : item,
                                    ),
                                  )
                                }
                              />
                            </Field>
                            <Field
                              label="Port"
                              htmlFor={`target-port-${index}`}
                              error={errors.find((e) => e.toLowerCase().includes('port'))}
                            >
                              <Input
                                id={`target-port-${index}`}
                                type="number"
                                value={target.port}
                                onChange={(event) =>
                                  update(
                                    'downstreamTargets',
                                    draft.downstreamTargets.map((item, i) =>
                                      i === index
                                        ? {
                                            ...item,
                                            port:
                                              event.target.value === ''
                                                ? ''
                                                : Number(event.target.value),
                                          }
                                        : item,
                                    ),
                                  )
                                }
                              />
                            </Field>
                            <Field label="Scheme" htmlFor={`target-scheme-${index}`}>
                              <Input
                                id={`target-scheme-${index}`}
                                value={target.scheme}
                                onChange={(event) =>
                                  update(
                                    'downstreamTargets',
                                    draft.downstreamTargets.map((item, i) =>
                                      i === index ? { ...item, scheme: event.target.value } : item,
                                    ),
                                  )
                                }
                              />
                            </Field>
                            <Field label="Path" htmlFor={`target-path-${index}`}>
                              <Input
                                id={`target-path-${index}`}
                                value={target.path}
                                onChange={(event) =>
                                  update(
                                    'downstreamTargets',
                                    draft.downstreamTargets.map((item, i) =>
                                      i === index ? { ...item, path: event.target.value } : item,
                                    ),
                                  )
                                }
                              />
                            </Field>
                            <div className="flex items-end pb-1">
                              <Button
                                type="button"
                                variant="ghost"
                                size="sm"
                                onClick={() =>
                                  update(
                                    'downstreamTargets',
                                    draft.downstreamTargets.filter((_, i) => i !== index),
                                  )
                                }
                                disabled={draft.downstreamTargets.length === 1}
                                aria-label={`Remove target ${index + 1}`}
                              >
                                Remove
                              </Button>
                            </div>
                          </div>
                        )
                      })}

                      {stepErrors.find((e) => e.startsWith('At least one')) ? (
                        <p className="text-xs text-destructive">
                          {stepErrors.find((e) => e.startsWith('At least one'))}
                        </p>
                      ) : null}
                      {stepErrors.find((e) => e.startsWith('A route cannot')) ? (
                        <p className="text-xs text-destructive">
                          {stepErrors.find((e) => e.startsWith('A route cannot'))}
                        </p>
                      ) : null}
                    </div>
                  </>
                ) : null}

                {step.id === 'authentication' ? (
                  <div className="space-y-3">
                    <Field
                      label="Allowed scopes"
                      htmlFor="route-scope"
                      hint="One scope per line, or comma separated. Leave empty for an open route."
                    >
                      <Input
                        id="route-scope"
                        value={scopeInput}
                        onChange={(event) => setScopeInput(event.target.value)}
                        placeholder="users.read, users.write"
                      />
                    </Field>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        const scopes = scopeInput
                          .split(/[\s,]+/)
                          .map((scope) => scope.trim())
                          .filter(Boolean)
                        if (scopes.length > 0) update('allowedScopes', scopes)
                        setScopeInput('')
                      }}
                    >
                      Add scopes
                    </Button>
                    {draft.allowedScopes.length > 0 ? (
                      <ul className="flex flex-wrap gap-2">
                        {draft.allowedScopes.map((scope) => (
                          <li key={scope}>
                            <Badge variant="secondary" className="gap-1">
                              {scope}
                              <button
                                type="button"
                                onClick={() =>
                                  update(
                                    'allowedScopes',
                                    draft.allowedScopes.filter((item) => item !== scope),
                                  )
                                }
                                aria-label={`Remove ${scope}`}
                                className="underline underline-offset-2"
                              >
                                ×
                              </button>
                            </Badge>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="text-sm text-muted-foreground">No scopes — route is open.</p>
                    )}
                  </div>
                ) : null}

                {step.id === 'rate-limiting' ? (
                  <ToggleField
                    id="ratelimit-enabled"
                    label="Enable rate limiting"
                    checked={draft.rateLimit.enabled}
                    onChange={(checked) =>
                      setDraft((current) => ({
                        ...current,
                        rateLimit: { ...current.rateLimit, enabled: checked },
                      }))
                    }
                  >
                    {draft.rateLimit.enabled ? (
                      <div className="grid gap-3 sm:grid-cols-2">
                        <Field
                          label="Limit"
                          htmlFor="ratelimit-limit"
                          error={stepErrors.find((e) => e.startsWith('Limit'))}
                        >
                          <Input
                            id="ratelimit-limit"
                            type="number"
                            value={draft.rateLimit.limit}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                rateLimit: {
                                  ...current.rateLimit,
                                  limit:
                                    event.target.value === ''
                                      ? ''
                                      : Number(event.target.value),
                                },
                              }))
                            }
                          />
                        </Field>
                        <Field label="Period" htmlFor="ratelimit-period">
                          <select
                            id="ratelimit-period"
                            className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                            value={draft.rateLimit.period}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                rateLimit: {
                                  ...current.rateLimit,
                                  period: event.target.value as RouteDraft['rateLimit']['period'],
                                },
                              }))
                            }
                          >
                            {periodOptions(draft.rateLimit.period).map((period) => (
                              <option key={period} value={period}>
                                {period}
                              </option>
                            ))}
                          </select>
                        </Field>
                      </div>
                    ) : null}
                  </ToggleField>
                ) : null}

                {step.id === 'qos' ? (
                  <ToggleField
                    id="qos-enabled"
                    label="Enable QoS settings"
                    checked={draft.qos.enabled}
                    onChange={(checked) =>
                      setDraft((current) => ({
                        ...current,
                        qos: { ...current.qos, enabled: checked },
                      }))
                    }
                  >
                    {draft.qos.enabled ? (
                      <div className="grid gap-3 sm:grid-cols-2">
                        <Field
                          label="Timeout (seconds)"
                          htmlFor="qos-timeout"
                          error={stepErrors.find((e) => e.startsWith('Timeout'))}
                        >
                          <Input
                            id="qos-timeout"
                            type="number"
                            value={draft.qos.timeoutSeconds}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                qos: {
                                  ...current.qos,
                                  timeoutSeconds:
                                    event.target.value === ''
                                      ? ''
                                      : Number(event.target.value),
                                },
                              }))
                            }
                          />
                        </Field>
                        <Field
                          label="Circuit breaker timeout (seconds)"
                          htmlFor="qos-breaker"
                          error={stepErrors.find((e) => e.startsWith('Circuit'))}
                          hint="Optional."
                        >
                          <Input
                            id="qos-breaker"
                            type="number"
                            value={draft.qos.circuitBreakerTimeoutSeconds}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                qos: {
                                  ...current.qos,
                                  circuitBreakerTimeoutSeconds:
                                    event.target.value === ''
                                      ? ''
                                      : Number(event.target.value),
                                },
                              }))
                            }
                          />
                        </Field>
                      </div>
                    ) : null}
                  </ToggleField>
                ) : null}

                {step.id === 'advanced' ? (
                  <div className="space-y-4">
                    <div className="space-y-3 border-b pb-4">
                      <div className="space-y-1.5 max-w-xs">
                        <Label htmlFor="route-priority">Priority</Label>
                        <Input
                          id="route-priority"
                          type="number"
                          min={0}
                          max={1000}
                          value={draft.priority}
                          onChange={(event) =>
                            update(
                              'priority',
                              event.target.value === '' ? 0 : Number(event.target.value),
                            )
                          }
                        />
                        <p className="text-xs text-muted-foreground">
                          Higher is matched first when routes overlap. Routes with
                          equal priority fall back to their order in the configuration.
                        </p>
                      </div>

                      <div className="flex items-center gap-2">
                        <Checkbox
                          id="route-case-sensitive"
                          checked={draft.routeIsCaseSensitive}
                          onCheckedChange={(checked) =>
                            update('routeIsCaseSensitive', checked === true)
                          }
                        />
                        <Label htmlFor="route-case-sensitive">
                          Match the path and host case-sensitively
                        </Label>
                      </div>
                      <p className="text-xs text-muted-foreground">
                        Off means <code className="font-mono">/api/Users</code> and{' '}
                        <code className="font-mono">/api/users</code> are the same route,
                        and whichever is listed first wins.
                      </p>
                    </div>
                    <ToggleField
                      id="cache-enabled"
                      label="Enable response caching"
                      checked={draft.cache.enabled}
                      onChange={(checked) =>
                        setDraft((current) => ({
                          ...current,
                          cache: { ...current.cache, enabled: checked },
                        }))
                      }
                    >
                      {draft.cache.enabled ? (
                        <Field
                          label="TTL (seconds)"
                          htmlFor="cache-ttl"
                          error={stepErrors.find((e) => e.startsWith('Cache'))}
                        >
                          <Input
                            id="cache-ttl"
                            type="number"
                            value={draft.cache.ttlSeconds}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                cache: {
                                  ...current.cache,
                                  ttlSeconds:
                                    event.target.value === ''
                                      ? ''
                                      : Number(event.target.value),
                                },
                              }))
                            }
                          />
                        </Field>
                      ) : null}
                    </ToggleField>

                    <ToggleField
                      id="lb-enabled"
                      label="Enable load balancing"
                      checked={draft.loadBalancer.enabled}
                      onChange={(checked) =>
                        setDraft((current) => ({
                          ...current,
                          loadBalancer: { ...current.loadBalancer, enabled: checked },
                        }))
                      }
                    >
                      {draft.loadBalancer.enabled ? (
                        <Field
                          label="Algorithm"
                          htmlFor="lb-algorithm"
                          error={stepErrors.find((e) => e.startsWith('A load'))}
                        >
                          <select
                            id="lb-algorithm"
                            className="h-9 w-full max-w-xs rounded-md border bg-background px-3 text-sm"
                            value={draft.loadBalancer.algorithm}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                loadBalancer: {
                                  ...current.loadBalancer,
                                  algorithm: event.target.value,
                                },
                              }))
                            }
                          >
                            {loadBalancerOptions(draft.loadBalancer.algorithm).map((algorithm) => (
                              <option key={algorithm} value={algorithm}>
                                {algorithm}
                              </option>
                            ))}
                          </select>
                        </Field>
                      ) : null}
                    </ToggleField>
                    <div className="space-y-4 border-t pt-4">
                      <div>
                        <h3 className="text-sm font-medium">Downstream request</h3>
                        <p className="text-xs text-muted-foreground">
                          How the gateway calls the service: which verb it uses, which
                          protocol version it asks for, and how it checks the
                          certificate.
                        </p>
                      </div>

                      <Field
                        label="Downstream method"
                        htmlFor="transport-downstream-method"
                        hint="Rewrites the verb on the way to the service. Leave empty to keep the upstream verb. Ocelot takes a single verb here, not a list."
                      >
                        <select
                          id="transport-downstream-method"
                          className="h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm"
                          value={transport.downstreamMethod}
                          onChange={(event) =>
                            setDraft((current) => ({
                              ...current,
                              transport: {
                                ...transport,
                                downstreamMethod: event.target.value,
                              },
                            }))
                          }
                        >
                          <option value="">Keep {draft.method}</option>
                          {HTTP_METHODS.map((method) => (
                            <option key={method} value={method}>
                              {method}
                            </option>
                          ))}
                        </select>
                      </Field>

                      <Field
                        label="Downstream path template"
                        htmlFor="transport-downstream-path"
                        hint="Rewrite the path on the way to the service. Leave empty to forward it unchanged. A placeholder is filled from the upstream path, so it has to appear there too."
                      >
                        <Input
                          id="transport-downstream-path"
                          placeholder="/{everything}"
                          value={transport.downstreamTemplate}
                          onChange={(event) =>
                            setDraft((current) => ({
                              ...current,
                              transport: {
                                ...transport,
                                downstreamTemplate: event.target.value,
                              },
                            }))
                          }
                        />
                      </Field>

                      <div className="grid gap-4 sm:grid-cols-2">
                        <Field
                          label="HTTP version"
                          htmlFor="transport-http-version"
                          error={stepErrors.find((e) => e.startsWith('Downstream HTTP version'))}
                          hint="Empty leaves the framework default, which is not the same as asking for 1.1."
                        >
                          <select
                            id="transport-http-version"
                            className="h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm"
                            value={transport.downstreamHttpVersion}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                transport: {
                                  ...transport,
                                  downstreamHttpVersion: event.target.value,
                                },
                              }))
                            }
                          >
                            <option value="">Framework default</option>
                            {DOWNSTREAM_HTTP_VERSIONS.map((version) => (
                              <option key={version} value={version}>
                                {version}
                              </option>
                            ))}
                          </select>
                        </Field>

                        <Field
                          label="Version policy"
                          htmlFor="transport-version-policy"
                          error={stepErrors.find(
                            (e) => e.startsWith('HTTP version policy') || e.startsWith('An HTTP version policy'),
                          )}
                          hint="Without a policy, asking for 2.0 over plain HTTP negotiates down to 1.1 and the gateway logs a protocol error."
                        >
                          <select
                            id="transport-version-policy"
                            className="h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm"
                            value={transport.downstreamHttpVersionPolicy}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                transport: {
                                  ...transport,
                                  downstreamHttpVersionPolicy: event.target.value,
                                },
                              }))
                            }
                          >
                            <option value="">None</option>
                            {DOWNSTREAM_HTTP_VERSION_POLICIES.map((policy) => (
                              <option key={policy} value={policy}>
                                {policy}
                              </option>
                            ))}
                          </select>
                        </Field>
                      </div>

                      <Field
                        label="Timeout (seconds)"
                        htmlFor="transport-timeout"
                        error={stepErrors.find((e) => e.startsWith('Timeout'))}
                        hint="Bounds the whole downstream call. Separate from the QoS timeout, which is the retry policy."
                      >
                        <Input
                          id="transport-timeout"
                          type="number"
                          min={1}
                          max={86400}
                          value={transport.timeoutSeconds}
                          onChange={(event) =>
                            setDraft((current) => ({
                              ...current,
                              transport: {
                                ...transport,
                                timeoutSeconds: event.target.value,
                              },
                            }))
                          }
                        />
                      </Field>

                      <div className="flex items-center gap-2">
                        <Checkbox
                          id="transport-accept-any-cert"
                          checked={transport.acceptAnyServerCertificate}
                          onCheckedChange={(checked) =>
                            setDraft((current) => ({
                              ...current,
                              transport: {
                                ...transport,
                                acceptAnyServerCertificate: checked === true,
                              },
                            }))
                          }
                        />
                        <Label htmlFor="transport-accept-any-cert">
                          Accept any TLS certificate
                        </Label>
                      </div>
                      <p className="text-xs text-muted-foreground">
                        Ocelot&rsquo;s own documentation calls this a security risk. Use
                        it for self-signed certificates in local development only.
                      </p>

                      <Field
                        label="Delegating handlers"
                        htmlFor="transport-handlers"
                        error={stepErrors.find((e) => e.includes('elegating handler'))}
                        hint="One per line. Each is registered in the gateway, so a name that is not registered stops it from starting."
                      >
                        <textarea
                          id="transport-handlers"
                          rows={3}
                          className="w-full rounded-md border border-input bg-transparent px-3 py-2 font-mono text-sm"
                          value={transport.delegatingHandlers.join('\n')}
                          onChange={(event) =>
                            setDraft((current) => ({
                              ...current,
                              transport: {
                                ...transport,
                                delegatingHandlers: event.target.value.split('\n'),
                              },
                            }))
                          }
                        />
                      </Field>

                      <ToggleField
                        id="transport-http-client"
                        label="Configure the HTTP client"
                        checked={transport.httpClient.enabled}
                        onChange={(checked) =>
                          setDraft((current) => ({
                            ...current,
                            transport: {
                              ...transport,
                              httpClient: { ...transport.httpClient, enabled: checked },
                            },
                          }))
                        }
                      >
                        <div className="space-y-3">
                          <div className="grid gap-4 sm:grid-cols-2">
                            <Field
                              label="Max connections per server"
                              htmlFor="transport-max-connections"
                              error={stepErrors.find((e) =>
                                e.startsWith('Max connections per server'),
                              )}
                              hint="Empty leaves the framework default."
                            >
                              <Input
                                id="transport-max-connections"
                                type="number"
                                min={1}
                                value={transport.httpClient.maxConnectionsPerServer}
                                onChange={(event) =>
                                  setDraft((current) => ({
                                    ...current,
                                    transport: {
                                      ...transport,
                                      httpClient: {
                                        ...transport.httpClient,
                                        maxConnectionsPerServer: event.target.value,
                                      },
                                    },
                                  }))
                                }
                              />
                            </Field>

                            <Field
                              label="Pooled connection lifetime (seconds)"
                              htmlFor="transport-connection-lifetime"
                              error={stepErrors.find((e) =>
                                e.startsWith('Pooled connection lifetime'),
                              )}
                              hint="Empty leaves the framework default."
                            >
                              <Input
                                id="transport-connection-lifetime"
                                type="number"
                                min={1}
                                value={transport.httpClient.pooledConnectionLifetimeSeconds}
                                onChange={(event) =>
                                  setDraft((current) => ({
                                    ...current,
                                    transport: {
                                      ...transport,
                                      httpClient: {
                                        ...transport.httpClient,
                                        pooledConnectionLifetimeSeconds: event.target.value,
                                      },
                                    },
                                  }))
                                }
                              />
                            </Field>
                          </div>

                          {clientToggles.map(({ field, label }) => (
                            <div key={field} className="flex items-center gap-2">
                              <Checkbox
                                id={`transport-${field}`}
                                checked={transport.httpClient[field]}
                                onCheckedChange={(checked) =>
                                  setDraft((current) => ({
                                    ...current,
                                    transport: {
                                      ...transport,
                                      httpClient: {
                                        ...transport.httpClient,
                                        [field]: checked === true,
                                      },
                                    },
                                  }))
                                }
                              />
                              <Label htmlFor={`transport-${field}`}>{label}</Label>
                            </div>
                          ))}
                        </div>
                      </ToggleField>

                      <div className="space-y-4 border-t pt-4">
                        <div>
                          <h3 className="text-sm font-medium">Header transformations</h3>
                          <p className="text-xs text-muted-foreground">
                            One rule per line, as <code className="font-mono">name: value</code>.
                            A value may contain Ocelot placeholders such as{' '}
                            <code className="font-mono">{'{UpstreamHost}'}</code> or{' '}
                            <code className="font-mono">{'{RemoteIpAddress}'}</code>, which are
                            filled per request. Anything after the first colon is the
                            value, so a URL is fine.
                          </p>
                        </div>

                        <Field
                          label="Rewrite on the way in (upstream)"
                          htmlFor="headers-transform"
                          error={stepErrors.find(
                            (e) => e.startsWith('Upstream header rule') || e.startsWith('Header'),
                          )}
                          hint="Rewrites a header the caller sent. Sent as Ocelot's UpstreamHeaderTransform."
                        >
                          <textarea
                            id="headers-transform"
                            rows={3}
                            className="w-full rounded-md border border-input bg-transparent px-3 py-2 font-mono text-sm"
                            placeholder={'X-Original-Host: {UpstreamHost}\nX-Forwarded-For: {RemoteIpAddress}'}
                            value={headers.transform}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                headers: { ...headers, transform: event.target.value },
                              }))
                            }
                          />
                        </Field>

                        <Field
                          label="Add on the way out (downstream)"
                          htmlFor="headers-add"
                          error={stepErrors.find((e) => e.startsWith('Downstream header rule'))}
                          hint="Sent as Ocelot's DownstreamHeaderTransform."
                        >
                          <textarea
                            id="headers-add"
                            rows={3}
                            className="w-full rounded-md border border-input bg-transparent px-3 py-2 font-mono text-sm"
                            placeholder={'X-Downstream-Service: orders'}
                            value={headers.add}
                            onChange={(event) =>
                              setDraft((current) => ({
                                ...current,
                                headers: { ...headers, add: event.target.value },
                              }))
                            }
                          />
                        </Field>
                      </div>
                    </div>
                  </div>
                ) : null}

                {step.id === 'review' ? (
                  <div className="space-y-4">
                    <dl className="grid gap-3 sm:grid-cols-[180px_1fr] text-sm">
                      <ReviewRow label="Key" value={draft.key} />
                      <ReviewRow label="Method" value={draft.method} />
                      <ReviewRow label="Upstream path" value={draft.upstreamPath} />
                      <ReviewRow label="Host" value={draft.host || '—'} />
                      <ReviewRow
                        label="Service"
                        value={
                          services.data?.services.find((s) => s.id === draft.serviceId)?.name ??
                          '—'
                        }
                      />
                      <ReviewRow
                        label="Downstream"
                        value={draft.downstreamTargets
                          .map(
                            (target) =>
                              `${target.scheme}://${target.host}:${target.port}${target.path}`,
                          )
                          .join(', ')}
                      />
                      <ReviewRow
                        label="Authentication"
                        value={draft.allowedScopes.length > 0 ? draft.allowedScopes.join(', ') : '—'}
                      />
                      <ReviewRow
                        label="Rate limiting"
                        value={
                          draft.rateLimit.enabled
                            ? `${draft.rateLimit.limit} / ${draft.rateLimit.period}`
                            : '—'
                        }
                      />
                      <ReviewRow
                        label="QoS"
                        value={
                          draft.qos.enabled
                            ? `timeout ${draft.qos.timeoutSeconds}s${
                                draft.qos.circuitBreakerTimeoutSeconds
                                  ? `, breaker ${draft.qos.circuitBreakerTimeoutSeconds}s`
                                  : ''
                              }`
                            : '—'
                        }
                      />
                      <ReviewRow
                        label="Cache"
                        value={draft.cache.enabled ? `${draft.cache.ttlSeconds}s TTL` : '—'}
                      />
                      <ReviewRow
                        label="Load balancing"
                        value={draft.loadBalancer.enabled ? draft.loadBalancer.algorithm : '—'}
                      />
                      <ReviewRow
                        label="Downstream method"
                        value={transport.downstreamMethod || `Keep ${draft.method}`}
                      />
                      <ReviewRow
                        label="Downstream path"
                        value={transport.downstreamTemplate || 'Forwarded unchanged'}
                      />
                      <ReviewRow
                        label="Upstream headers"
                        value={summariseRules(headers.transform) || '—'}
                      />
                      <ReviewRow
                        label="Downstream headers"
                        value={summariseRules(headers.add) || '—'}
                      />
                      <ReviewRow
                        label="HTTP version"
                        value={
                          transport.downstreamHttpVersion
                            ? `${transport.downstreamHttpVersion}${
                                transport.downstreamHttpVersionPolicy
                                  ? `, ${transport.downstreamHttpVersionPolicy}`
                                  : ''
                              }`
                            : 'Framework default'
                        }
                      />
                      <ReviewRow
                        label="Timeout"
                        value={transport.timeoutSeconds ? `${transport.timeoutSeconds}s` : '—'}
                      />
                      <ReviewRow
                        label="TLS check"
                        value={
                          transport.acceptAnyServerCertificate
                            ? 'Any certificate accepted'
                            : 'Verified'
                        }
                      />
                      <ReviewRow
                        label="Delegating handlers"
                        value={
                          transport.delegatingHandlers.filter((h) => h.trim() !== '').join(', ') || '—'
                        }
                      />
                      <ReviewRow
                        label="HTTP client"
                        value={transport.httpClient.enabled ? 'Custom' : '—'}
                      />
                    </dl>

                    {/* Whichever mutation ran: the save path differs by mode. */}
                    {saveError ? <ErrorState {...toRouteError(saveError)} /> : null}

                    {validateDraft.isError ? (
                      <ErrorState
                        title="Could not reach the validator"
                        message={
                          validateDraft.error instanceof Error
                            ? validateDraft.error.message
                            : String(validateDraft.error)
                        }
                      />
                    ) : null}

                    {validateDraft.data && !validateDraft.data.isValid ? (
                      <div className="space-y-2">
                        <p className="text-sm font-medium text-destructive">
                          {validateDraft.data.errors.length === 1
                            ? 'One problem to fix before saving:'
                            : `${validateDraft.data.errors.length} problems to fix before saving:`}
                        </p>
                        <ul className="space-y-1">
                          {validateDraft.data.errors.map((error) => (
                            <li key={`${error.field ?? 'route'}-${error.code}`} className="text-sm">
                              <button
                                type="button"
                                className="text-left underline underline-offset-2"
                                onClick={() => setStepIndex(stepIndexOf(stepForField(error.field)))}
                              >
                                {error.message}
                              </button>{' '}
                              <span className="text-muted-foreground">
                                — {WIZARD_STEPS[stepIndexOf(stepForField(error.field))].title}
                              </span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    ) : null}

                    {validateDraft.data?.isValid ? (
                      <p className="text-sm text-muted-foreground">
                        The API accepts this configuration.
                      </p>
                    ) : null}

                    <details className="rounded-md border p-3">
                      <summary className="cursor-pointer text-sm font-medium">
                        {isEdit
                          ? 'Replacement payload — anything absent is removed'
                          : 'Request payload'}
                      </summary>
                      <pre className="mt-2 overflow-x-auto text-xs">
                        {JSON.stringify(toCreateRequest(draft), null, 2)}
                      </pre>
                    </details>
                  </div>
                ) : null}
              </section>

              <div className="flex items-center justify-between">
                <Button
                  variant="outline"
                  onClick={() => setStepIndex((index) => Math.max(0, index - 1))}
                  disabled={stepIndex === 0 || isSaving}
                >
                  Back
                </Button>

                {isLastStep ? (
                  <Button
                    onClick={() => {
                      // Check the draft first, so a problem is reported against
                      // the field that caused it rather than after the fact.
                      validateDraft.mutate(draft, {
                        onSuccess: (result) => {
                          if (!result.isValid) {
                            setServerErrors(
                              Object.fromEntries(
                                result.errors.map((error) => [
                                  stepIndexOf(stepForField(error.field)),
                                  [error.message],
                                ]),
                              ),
                            )
                            return
                          }

                          setServerErrors({})

                          if (isEdit && routeId) {
                            updateRoute.mutate(
                              { id: routeId, draft },
                              { onSuccess: (saved) => navigate(`/routes/${saved.id}`) },
                            )
                            return
                          }

                          createRoute.mutate(draft, {
                            onSuccess: (created) => navigate(`/routes/${created.id}`),
                          })
                        },
                      })
                    }}
                    disabled={!canAdvance || isSaving || validateDraft.isPending}
                  >
                    {validateDraft.isPending
                      ? 'Checking…'
                      : isSaving
                        ? isEdit
                          ? 'Saving…'
                          : 'Creating…'
                        : isEdit
                          ? 'Save changes'
                          : 'Create route'}
                  </Button>
                ) : (
                  <Button
                    onClick={() => setStepIndex((index) => index + 1)}
                    disabled={!canAdvance}
                  >
                    Next
                  </Button>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </>
  )
}

export function CreateRouteWizardPage() {
  return <RouteWizard mode="create" />
}

/** Edits a stored route through the same steps. */
export function EditRouteWizardPage() {
  const { id } = useParams<{ id: string }>()
  return <RouteWizard mode="edit" routeId={id} />
}

/** Step index for a step id, so server errors can point at a step. */
function stepIndexOf(id: StepId): number {
  const index = WIZARD_STEPS.findIndex((step) => step.id === id)
  return index === -1 ? WIZARD_STEPS.length - 1 : index
}

function ReviewRow({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-mono text-xs break-all">{value}</dd>
    </>
  )
}

function ToggleField({
  id,
  label,
  checked,
  onChange,
  children,
}: {
  id: string
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
  children?: React.ReactNode
}) {
  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <Checkbox id={id} checked={checked} onCheckedChange={onChange} />
        <Label htmlFor={id}>{label}</Label>
      </div>
      {checked ? <div className="pl-6">{children}</div> : null}
    </div>
  )
}

/** A missing route is an expected outcome, not a failure to report as an error. */
function isNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.isNotFound
}
