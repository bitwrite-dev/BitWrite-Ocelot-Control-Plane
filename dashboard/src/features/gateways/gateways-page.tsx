import { useState } from 'react'
import { CircleAlert, Info, Pencil, Plus, Trash2 } from 'lucide-react'

import { StatusBadge } from '@/components/status-badge'
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
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type { GatewayResponse } from '@/api'
import { isPermissionError } from '@/lib/api-error'

import {
  GATEWAY_STATUSES,
  HEARTBEAT_STALE_AFTER_MINUTES,
  emptyGatewayDraft,
  gatewayErrors,
  toGatewayRequest,
  useGatewayDeletionEligibility,
  useGatewayMutations,
  useGateways,
  type GatewayDraft,
} from './queries'

const PAGE_SIZE = 20

/**
 * Gateways: the targets a snapshot is published to.
 *
 * The status shown is the one the control plane records, not live health. Live
 * health belongs to Monitoring, and the two have different sources — presenting
 * a recorded label as if it were a heartbeat would be misleading in the
 * direction that matters, since a stale "Active" looks like a healthy gateway.
 */
export function GatewaysPage() {
  const [page, setPage] = useState(1)
  const { data, isPending, isError, error, refetch } = useGateways(page, PAGE_SIZE)

  if (isPending) return <GatewaysSkeleton />
  if (isError || !data) {
    return (
      <Alert variant="destructive">
        <CircleAlert aria-hidden="true" className="size-4" />
        <AlertTitle>Could not load gateways</AlertTitle>
        <AlertDescription>
          {error instanceof Error ? error.message : String(error ?? 'No gateways were returned')}
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
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold">Gateways</h1>
          <p className="text-sm text-muted-foreground">
            The targets a published snapshot is delivered to. The status shown is the one
            this control plane records; live health and metrics are on the Monitoring page.
          </p>
        </div>
        <NewGatewayButton />
      </header>

      <GatewayTable gateways={data.gateways} />

      <Pagination
        page={data.page}
        pageSize={data.pageSize}
        totalCount={data.totalCount}
        onPage={setPage}
      />
    </div>
  )
}

function GatewayTable({ gateways }: { gateways: GatewayResponse[] }) {
  if (gateways.length === 0) {
    return (
      <Card>
        <CardContent className="pt-6 text-sm text-muted-foreground">
          No gateways are registered yet. One is needed before a snapshot can be published
          anywhere.
        </CardContent>
      </Card>
    )
  }

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Last report</TableHead>
            <TableHead>Description</TableHead>
            <TableHead className="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {gateways.map((gateway) => (
            <GatewayRow key={gateway.id} gateway={gateway} />
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

function GatewayRow({ gateway }: { gateway: GatewayResponse }) {
  const [editing, setEditing] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const { setStatus } = useGatewayMutations()

  return (
    <>
      <TableRow>
        <TableCell className="font-medium">{gateway.name}</TableCell>
        <TableCell>
          <StatusBadge status={gateway.status} />
        </TableCell>
        <TableCell className={isStale(gateway.lastHeartbeat) ? 'text-destructive' : 'text-muted-foreground'}>
          {describeLastHeartbeat(gateway.lastHeartbeat)}
        </TableCell>
        <TableCell className="max-w-md text-muted-foreground">
          {gateway.description || '—'}
        </TableCell>
        <TableCell className="text-right">
          <div className="flex items-center justify-end gap-2">
            <StatusSelect
              gateway={gateway}
              onChange={(status) => setStatus.mutate({ id: gateway.id, status })}
            />
            <Button
              variant="outline"
              size="sm"
              onClick={() => setEditing(true)}
              aria-label={`Edit ${gateway.name}`}
            >
              <Pencil aria-hidden="true" className="size-3" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setConfirmingDelete(true)}
              aria-label={`Delete ${gateway.name}`}
            >
              <Trash2 aria-hidden="true" className="size-3" />
            </Button>
          </div>
        </TableCell>
      </TableRow>

      {editing ? (
        <GatewayFormRow
          gateway={gateway}
          onDone={() => setEditing(false)}
        />
      ) : null}

      <DeleteGatewayDialog
        gateway={gateway}
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
      />
    </>
  )
}

/**
 * The recorded status, offered as a select.
 *
 * Labelled so it does not read as a power switch: the API records a label, and
 * there is no shutdown state to toggle. Changing this does not stop a gateway
 * serving traffic.
 */
function StatusSelect({
  gateway,
  onChange,
}: {
  gateway: GatewayResponse
  onChange: (status: string) => void
}) {
  return (
    <Select value={gateway.status} onValueChange={onChange}>
      <SelectTrigger
        className="w-[170px]"
        aria-label={`Recorded status for ${gateway.name}`}
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {GATEWAY_STATUSES.map((status) => (
          <SelectItem key={status} value={status}>
            {status}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

function NewGatewayButton() {
  const [open, setOpen] = useState(false)

  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Plus aria-hidden="true" className="mr-1 size-4" />
        New gateway
      </Button>
      {open ? <GatewayFormDialog onDone={() => setOpen(false)} /> : null}
    </>
  )
}

function GatewayFormDialog({ onDone }: { onDone: () => void }) {
  const [draft, setDraft] = useState<GatewayDraft>(emptyGatewayDraft)
  const { create } = useGatewayMutations()
  const errors = gatewayErrors(draft)

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    if (Object.keys(errors).length > 0) return
    create.mutate(toGatewayRequest(draft), { onSuccess: onDone })
  }

  return (
    <AlertDialog open onOpenChange={(next) => !next && onDone()}>
      <AlertDialogContent>
        <form onSubmit={submit} className="space-y-4">
          <AlertDialogHeader>
            <AlertDialogTitle>Register a gateway</AlertDialogTitle>
            <AlertDialogDescription>
              A gateway is a target a published snapshot can be delivered to.
            </AlertDialogDescription>
          </AlertDialogHeader>

          <GatewayFields draft={draft} errors={errors} onChange={setDraft} />

          {create.error ? <MutationError error={create.error} /> : null}

          <AlertDialogFooter>
            <AlertDialogCancel type="button">Cancel</AlertDialogCancel>
            <Button
              type="submit"
              disabled={create.isPending || Object.keys(errors).length > 0}
            >
              {create.isPending ? 'Registering…' : 'Register'}
            </Button>
          </AlertDialogFooter>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  )
}

function GatewayFormRow({
  gateway,
  onDone,
}: {
  gateway: GatewayResponse
  onDone: () => void
}) {
  const [draft, setDraft] = useState<GatewayDraft>({
    name: gateway.name,
    description: gateway.description ?? '',
  })
  const { update } = useGatewayMutations()
  const errors = gatewayErrors(draft)

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    if (Object.keys(errors).length > 0) return
    update.mutate({ id: gateway.id, body: toGatewayRequest(draft) }, { onSuccess: onDone })
  }

  return (
    <TableRow>
      <TableCell colSpan={4}>
        <form onSubmit={submit} className="space-y-3 py-2">
          <GatewayFields draft={draft} errors={errors} onChange={setDraft} />
          {update.error ? <MutationError error={update.error} /> : null}
          <div className="flex items-center gap-2">
            <Button type="submit" size="sm" disabled={update.isPending || Object.keys(errors).length > 0}>
              {update.isPending ? 'Saving…' : 'Save'}
            </Button>
            <Button type="button" size="sm" variant="ghost" onClick={onDone}>
              Cancel
            </Button>
          </div>
        </form>
      </TableCell>
    </TableRow>
  )
}

function GatewayFields({
  draft,
  errors,
  onChange,
}: {
  draft: GatewayDraft
  errors: Record<string, string>
  onChange: (draft: GatewayDraft) => void
}) {
  return (
    <div className="space-y-3">
      <div className="space-y-1.5">
        <Label htmlFor="gateway-name">Name</Label>
        <Input
          id="gateway-name"
          value={draft.name}
          aria-invalid={Boolean(errors.name)}
          onChange={(event) => onChange({ ...draft, name: event.target.value })}
        />
        {errors.name ? <FieldError>{errors.name}</FieldError> : null}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="gateway-description">Description</Label>
        <textarea
          id="gateway-description"
          rows={3}
          className="w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm"
          value={draft.description}
          aria-invalid={Boolean(errors.description)}
          onChange={(event) => onChange({ ...draft, description: event.target.value })}
        />
        {errors.description ? <FieldError>{errors.description}</FieldError> : null}
      </div>
    </div>
  )
}

/**
 * The delete confirmation, which asks the API whether the gateway can be
 * removed rather than assuming it can.
 *
 * A gateway that has received a publication cannot be deleted: the snapshot is an
 * immutable hashed record that refers to it. Learning that only from the refused
 * delete would mean the operator had already committed to the action.
 */
function DeleteGatewayDialog({
  gateway,
  open,
  onOpenChange,
}: {
  gateway: GatewayResponse
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { remove } = useGatewayMutations()
  const { data, isPending } = useGatewayDeletionEligibility(open ? gateway.id : undefined, open)

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {gateway.name}?</AlertDialogTitle>
          <AlertDialogDescription>
            The gateway is removed from this control plane. This cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>

        {isPending ? (
          <p className="text-sm text-muted-foreground">Checking whether it can be removed…</p>
        ) : data?.hasBeenPublishedTo ? (
          <Alert variant="destructive">
            <CircleAlert aria-hidden="true" className="size-4" />
            <AlertTitle>Cannot be deleted</AlertTitle>
            <AlertDescription>{data.reason}</AlertDescription>
          </Alert>
        ) : (
          <Alert>
            <Info aria-hidden="true" className="size-4" />
            <AlertDescription>
              This gateway has never received a publication, so it can be removed.
            </AlertDescription>
          </Alert>
        )}

        {remove.error ? <MutationError error={remove.error} /> : null}

        <AlertDialogFooter>
          <AlertDialogCancel type="button">Cancel</AlertDialogCancel>
          <AlertDialogAction
            type="button"
            disabled={isPending || data?.hasBeenPublishedTo || remove.isPending}
            onClick={() => remove.mutate(gateway.id, { onSuccess: () => onOpenChange(false) })}
          >
            {remove.isPending ? 'Deleting…' : 'Delete'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

/**
 * Whether the report is too old for the status beside it to mean anything.
 */
function isStale(lastHeartbeat: string | null): boolean {
  if (!lastHeartbeat) return true

  const parsed = new Date(lastHeartbeat)
  if (Number.isNaN(parsed.getTime())) return true

  return Date.now() - parsed.getTime() > HEARTBEAT_STALE_AFTER_MINUTES * 60_000
}

/**
 * How long ago the gateway last reported, and whether that is recent enough to
 * believe the status beside it.
 *
 * The status is a label the control plane records and nothing updates it on its
 * own, so a stale report is the operator's only signal that it no longer
 * describes the gateway. This is deliberately not a "last seen" in the sense of
 * activity — it is only ever updated by a report.
 */
function describeLastHeartbeat(lastHeartbeat: string | null): string {
  if (!lastHeartbeat) return 'Never reported'

  const parsed = new Date(lastHeartbeat)
  if (Number.isNaN(parsed.getTime())) return 'Never reported'

  const ageMs = Date.now() - parsed.getTime()
  const minutes = Math.floor(ageMs / 60000)

  if (ageMs < 0) return 'Just now'
  if (minutes < 1) return 'Just now'
  if (minutes < 60) return `${minutes} min ago`

  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} h ago`

  const days = Math.floor(hours / 24)
  if (days < 30) return `${days} d ago`
  return parsed.toLocaleDateString()
}

function MutationError({ error }: { error: unknown }) {
  const message = error instanceof Error ? error.message : String(error)

  return (
    <Alert variant="destructive">
      <CircleAlert aria-hidden="true" className="size-4" />
      <AlertTitle>
        {isPermissionError(error) ? 'Not permitted' : 'The request was refused'}
      </AlertTitle>
      <AlertDescription>
        {isPermissionError(error)
          ? 'Managing gateways needs the GatewayManager or Admin role.'
          : message}
      </AlertDescription>
    </Alert>
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

function Pagination({
  page,
  pageSize,
  totalCount,
  onPage,
}: {
  page: number
  pageSize: number
  totalCount: number
  onPage: (page: number) => void
}) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  if (totalPages <= 1) return null

  return (
    <div className="flex items-center justify-between text-sm">
      <p className="text-muted-foreground">
        Page {page} of {totalPages} · {totalCount} gateways
      </p>
      <div className="flex gap-2">
        <Button
          variant="outline"
          size="sm"
          disabled={page <= 1}
          onClick={() => onPage(page - 1)}
        >
          Previous
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onPage(page + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  )
}

function GatewaysSkeleton() {
  return (
    <div className="space-y-4">
      <div className="h-8 w-40 animate-pulse rounded bg-muted" />
      <div className="h-48 w-full animate-pulse rounded bg-muted" />
    </div>
  )
}
