import type { ReactNode } from 'react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Skeleton } from '@/components/ui/skeleton'

/**
 * Shared page state components.
 *
 * The five list pages all need the same four states, so they are defined once
 * here rather than re-implemented per page.
 */

/** Shown while a request is in flight. */
export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="space-y-3" role="status" aria-live="polite" aria-busy="true">
      <span className="sr-only">{label}</span>
      <Skeleton className="h-8 w-full" />
      <Skeleton className="h-8 w-full" />
      <Skeleton className="h-8 w-3/4" />
    </div>
  )
}

/** Shown when a collection has no rows. */
export function EmptyState({
  title,
  description,
  action,
}: {
  title: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed p-10 text-center">
      <p className="font-medium">{title}</p>
      {description ? (
        <p className="max-w-md text-sm text-muted-foreground">{description}</p>
      ) : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  )
}

/** Shown when a request failed. Renders the correlation id for support. */
export function ErrorState({
  title = 'Something went wrong',
  message,
  correlationId,
  action,
}: {
  title?: string
  message?: string
  correlationId?: string
  action?: ReactNode
}) {
  return (
    <Alert variant="destructive" role="alert">
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription className="space-y-3">
        {message ? <p>{message}</p> : null}
        {correlationId ? (
          <p className="font-mono text-xs opacity-80">Correlation ID: {correlationId}</p>
        ) : null}
        {action}
      </AlertDescription>
    </Alert>
  )
}

/** Shown when the current user is not allowed to see a page at all. */
export function ForbiddenState() {
  return (
    <EmptyState
      title="You do not have access to this page"
      description="Ask an administrator for the required role."
    />
  )
}
