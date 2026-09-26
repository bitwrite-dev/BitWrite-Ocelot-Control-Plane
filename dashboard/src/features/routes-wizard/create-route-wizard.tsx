import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Check, CircleAlert } from 'lucide-react'

import { PageHeader } from '@/components/app-layout'
import { ErrorState, LoadingState } from '@/components/page-state'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Checkbox } from '@/components/ui/checkbox'
import {
  HTTP_METHODS,
  LOAD_BALANCER_ALGORITHMS,
  PERIODS,
  UNSUPPORTED_STEPS,
  WIZARD_STEPS,
  emptyRouteDraft,
  furthestReachableStep,
  toCreateRequest,
  type RouteDraft,
} from './wizard-model'
import { toRouteError } from '@/features/routes/queries'
import { useCreateRoute, useWizardServices } from './queries'

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

export function CreateRouteWizardPage() {
  const [stepIndex, setStepIndex] = useState(0)
  const [draft, setDraft] = useState<RouteDraft>(emptyRouteDraft)
  const [scopeInput, setScopeInput] = useState('')

  const services = useWizardServices()
  const createRoute = useCreateRoute()
  const navigate = useNavigate()

  const step = WIZARD_STEPS[stepIndex]
  const isLastStep = stepIndex === WIZARD_STEPS.length - 1
  const stepErrors = useMemo(() => step.validate(draft), [step, draft])
  const reachable = useMemo(() => furthestReachableStep(draft), [draft])

  const update = <K extends keyof RouteDraft>(key: K, value: RouteDraft[K]) =>
    setDraft((current) => ({ ...current, [key]: value }))

  const canAdvance = stepErrors.length === 0

  return (
    <>
      <PageHeader
        title="Create Route"
        description={`Step ${stepIndex + 1} of ${WIZARD_STEPS.length} — ${step.title}`}
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
                            {PERIODS.map((period) => (
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
                            {LOAD_BALANCER_ALGORITHMS.map((algorithm) => (
                              <option key={algorithm} value={algorithm}>
                                {algorithm}
                              </option>
                            ))}
                          </select>
                        </Field>
                      ) : null}
                    </ToggleField>
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
                    </dl>

                    {createRoute.error ? <ErrorState {...toRouteError(createRoute.error)} /> : null}

                    <details className="rounded-md border p-3">
                      <summary className="cursor-pointer text-sm font-medium">
                        Request payload
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
                  disabled={stepIndex === 0 || createRoute.isPending}
                >
                  Back
                </Button>

                {isLastStep ? (
                  <Button
                    onClick={() => {
                      createRoute.mutate(draft, {
                        onSuccess: (created) => navigate(`/routes/${created.id}`),
                      })
                    }}
                    disabled={!canAdvance || createRoute.isPending}
                  >
                    {createRoute.isPending ? 'Creating…' : 'Create route'}
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
