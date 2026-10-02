import { useState } from 'react'

import { ErrorState } from '@/components/page-state'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import {
  serviceErrorMessage,
  serviceErrorTitle,
  toServiceFieldErrors,
  type ServiceDraft,
} from './queries'

/** A duplicate host and port would be rejected by the domain, so catch it here. */
export function validateServiceDraft(draft: ServiceDraft): Record<string, string[]> {
  const errors: Record<string, string[]> = {}

  if (draft.name.trim().length === 0) {
    errors.name = ['Name is required']
  } else if (draft.name.trim().length > 200) {
    errors.name = ['Name must be 200 characters or fewer']
  }

  if (draft.description !== null && draft.description.length > 1000) {
    errors.description = ['Description must be 1000 characters or fewer']
  }

  if (draft.downstreamTargets.length === 0) {
    errors.downstreamTargets = ['A service needs at least one endpoint']
  }

  const seen = new Set<string>()
  draft.downstreamTargets.forEach((target, index) => {
    const host = target.host.trim()
    if (host.length === 0) {
      errors[`endpoints.${index}.host`] = ['Host is required']
    } else if (host.length > 200) {
      errors[`endpoints.${index}.host`] = ['Host must be 200 characters or fewer']
    }

    if (target.port === '') {
      errors[`endpoints.${index}.port`] = ['Port is required']
    } else if (!Number.isInteger(target.port) || target.port < 1 || target.port > 65535) {
      errors[`endpoints.${index}.port`] = ['Port must be between 1 and 65535']
    }

    if (target.weight === '') {
      errors[`endpoints.${index}.weight`] = ['Weight is required']
    } else if (!Number.isInteger(target.weight) || target.weight < 1 || target.weight > 1000) {
      // The API accepts 1–1000 and the domain refuses a non-positive weight; the
      // range is checked here so the reason arrives before a round trip.
      errors[`endpoints.${index}.weight`] = ['Weight must be between 1 and 1000']
    }

    if (host && target.port !== '') {
      const key = `${host.toLowerCase()}:${target.port}`
      if (seen.has(key)) {
        // The aggregate rejects duplicates, and the API error would not say
        // which row was at fault.
        errors[`endpoints.${index}.host`] = ['This endpoint is already listed']
      }
      seen.add(key)
    }
  })

  return errors
}

export function hasErrors(errors: Record<string, string[]>): boolean {
  return Object.keys(errors).length > 0
}

/**
 * Create or edit form for a service.
 *
 * Endpoints are host, port and weight, which is what `ServiceEndpoint` holds. The
 * API used to borrow the route's endpoint shape and answer with an invented scheme
 * and path it never stored, so the form kept two fields that quietly did nothing —
 * see #474. Weight was in the domain all along and could neither be seen nor set,
 * so it is editable here.
 */
export function ServiceForm({
  initial,
  submitLabel,
  pending,
  error,
  onSubmit,
  onCancel,
}: {
  initial: ServiceDraft
  submitLabel: string
  pending: boolean
  error: unknown
  onSubmit: (draft: ServiceDraft) => void
  onCancel: () => void
}) {
  const [draft, setDraft] = useState<ServiceDraft>(initial)
  const [showErrors, setShowErrors] = useState(false)

  const clientErrors = validateServiceDraft(draft)
  // Server errors win: they are the authority on what was actually rejected.
  const serverErrors = toServiceFieldErrors(error)
  const errors = hasErrors(serverErrors) ? serverErrors : showErrors ? clientErrors : {}

  const setEndpoint = (index: number, patch: Partial<ServiceDraft['downstreamTargets'][number]>) =>
    setDraft((current) => ({
      ...current,
      downstreamTargets: current.downstreamTargets.map((target, i) =>
        i === index
          ? {
              ...target,
              ...patch,
              port: patch.port === undefined ? target.port : patch.port,
            }
          : target,
      ),
    }))

  return (
    <form
      className="space-y-5"
      onSubmit={(event) => {
        event.preventDefault()
        setShowErrors(true)
        if (hasErrors(clientErrors)) return
        onSubmit(draft)
      }}
    >
      <div className="space-y-1.5">
        <Label htmlFor="service-name">Name</Label>
        <Input
          id="service-name"
          value={draft.name}
          onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))}
        />
        {errors.name?.map((message) => (
          <p key={message} className="text-xs text-destructive">
            {message}
          </p>
        ))}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="service-description">Description</Label>
        <textarea
          id="service-description"
          rows={3}
          className="w-full rounded-md border bg-background px-3 py-2 text-sm"
          value={draft.description ?? ''}
          onChange={(event) =>
            setDraft((current) => ({
              ...current,
              // An empty box means no description, not an empty string.
              description: event.target.value === '' ? null : event.target.value,
            }))
          }
        />
        {errors.description?.map((message) => (
          <p key={message} className="text-xs text-destructive">
            {message}
          </p>
        ))}
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <Label>Endpoints</Label>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() =>
              setDraft((current) => ({
                ...current,
                downstreamTargets: [
                  ...current.downstreamTargets,
                  { host: '', port: '', weight: 1 },
                ],
              }))
            }
          >
            Add endpoint
          </Button>
        </div>

        <ul className="space-y-2">
          {draft.downstreamTargets.map((endpoint, index) => (
            <li key={index} className="grid gap-2 sm:grid-cols-[2fr_1fr_1fr_auto]">
              <div className="space-y-1">
                <Input
                  aria-label={`Endpoint ${index + 1} host`}
                  value={endpoint.host}
                  onChange={(event) => setEndpoint(index, { host: event.target.value })}
                  placeholder="localhost"
                />
                {errors[`endpoints.${index}.host`]?.map((message) => (
                  <p key={message} className="text-xs text-destructive">
                    {message}
                  </p>
                ))}
              </div>
              <div className="space-y-1">
                <Input
                  aria-label={`Endpoint ${index + 1} port`}
                  type="number"
                  value={endpoint.port}
                  onChange={(event) =>
                    setEndpoint(index, {
                      port: event.target.value === '' ? '' : Number(event.target.value),
                    })
                  }
                  placeholder="5001"
                />
                {errors[`endpoints.${index}.port`]?.map((message) => (
                  <p key={message} className="text-xs text-destructive">
                    {message}
                  </p>
                ))}
              </div>
              <div className="space-y-1">
                <Input
                  aria-label={`Endpoint ${index + 1} weight`}
                  type="number"
                  value={endpoint.weight}
                  onChange={(event) =>
                    setEndpoint(index, {
                      weight: event.target.value === '' ? '' : Number(event.target.value),
                    })
                  }
                  placeholder="1"
                />
                {errors[`endpoints.${index}.weight`]?.map((message) => (
                  <p key={message} className="text-xs text-destructive">
                    {message}
                  </p>
                ))}
              </div>
              <div className="flex items-end pb-0.5">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  // The last one cannot go: a service always needs an endpoint.
                  disabled={draft.downstreamTargets.length === 1}
                  onClick={() =>
                    setDraft((current) => ({
                      ...current,
                      downstreamTargets: current.downstreamTargets.filter(
                        (_, i) => i !== index,
                      ),
                    }))
                  }
                >
                  Remove
                </Button>
              </div>
            </li>
          ))}
        </ul>

        {errors.downstreamTargets?.map((message) => (
          <p key={message} className="text-xs text-destructive">
            {message}
          </p>
        ))}

        <p className="text-xs text-muted-foreground">
          Each endpoint is a host and port. Paths belong to a route&apos;s downstream
          targets, not to a service.
        </p>
      </div>

      {error && !hasErrors(serverErrors) ? (
        <ErrorState title={serviceErrorTitle(error)} message={serviceErrorMessage(error)} />
      ) : null}

      {hasErrors(serverErrors) ? (
        <Alert variant="destructive" role="alert">
          <AlertTitle>{serviceErrorTitle(error)}</AlertTitle>
          <AlertDescription>
            <ul className="list-inside list-disc">
              {Object.entries(serverErrors).flatMap(([field, messages]) =>
                messages.map((message) => (
                  <li key={`${field}-${message}`}>{message}</li>
                )),
              )}
            </ul>
          </AlertDescription>
        </Alert>
      ) : null}

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={pending}>
          Cancel
        </Button>
        <Button type="submit" disabled={pending}>
          {pending ? 'Saving…' : submitLabel}
        </Button>
      </div>
    </form>
  )
}
