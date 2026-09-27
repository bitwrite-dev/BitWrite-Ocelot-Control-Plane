import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Plus, Trash2 } from 'lucide-react'

import { PageHeader } from '@/components/app-layout'
import { DataTable, type Column } from '@/components/data-table'
import { EmptyState, ErrorState } from '@/components/page-state'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import type { ServiceResponse } from '@/api'
import {
  emptyServiceDraft,
  serviceDraftFrom,
  toServiceRequest,
  useServiceMutations,
  useServiceRoutes,
  useServices,
  type ServiceDraft,
} from './queries'
import { ServiceForm } from './service-form'

const PAGE = 'page'
const PAGE_SIZE = 'pageSize'

/** Shared with the routes list so both tables offer the same sizes. */
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100]

export function ServicesPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()

  const page = Math.max(1, Number(searchParams.get(PAGE) ?? 1))
  const pageSize = Number(searchParams.get(PAGE_SIZE) ?? 20)

  const [editing, setEditing] = useState<ServiceDraft | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [deleting, setDeleting] = useState<ServiceResponse | null>(null)

  const services = useServices(page, pageSize)
  const mutations = useServiceMutations()

  // Loaded only while the delete confirmation is open, to warn about routes
  // that would be left pointing at nothing.
  const dependents = useServiceRoutes(deleting?.id, deleting !== null)

  const updateParams = (patch: Record<string, string | number>) => {
    const next = new URLSearchParams(searchParams)
    for (const [key, value] of Object.entries(patch)) next.set(key, String(value))
    setSearchParams(next)
  }

  const rows = services.data?.services ?? []
  const anyPending =
    mutations.create.isPending || mutations.update.isPending || mutations.remove.isPending

  const startCreate = () => {
    setEditing(emptyServiceDraft())
    setEditingId(null)
    mutations.create.reset()
    mutations.update.reset()
  }

  const startEdit = (service: ServiceResponse) => {
    setEditing(serviceDraftFrom(service))
    setEditingId(service.id)
    mutations.create.reset()
    mutations.update.reset()
  }

  const submit = (draft: ServiceDraft) => {
    const body = toServiceRequest(draft)
    const close = () => {
      setEditing(null)
      setEditingId(null)
    }

    if (editingId) {
      mutations.update.mutate(
        { id: editingId, body },
        {
          onSuccess: (saved) => {
            close()
            navigate(`/services/${saved.id}`)
          },
        },
      )
      return
    }

    mutations.create.mutate(body, { onSuccess: close })
  }

  const columns: Column<ServiceResponse>[] = [
    {
      key: 'name',
      header: 'Name',
      cell: (row) => <span className="font-medium">{row.name}</span>,
    },
    {
      key: 'endpoints',
      header: 'Endpoints',
      cell: (row) =>
        row.downstreamTargets.length > 0 ? (
          <span className="font-mono text-xs">
            {row.downstreamTargets.map((target) => `${target.host}:${target.port}`).join(', ')}
          </span>
        ) : (
          <span className="text-muted-foreground">None</span>
        ),
    },
    {
      key: 'description',
      header: 'Description',
      cell: (row) =>
        row.description ? (
          <span className="text-sm text-muted-foreground">{row.description}</span>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      key: 'actions',
      header: 'Actions',
      className: 'text-right',
      cell: (row) => (
        <div
          className="flex justify-end gap-2"
          // The row itself navigates, so the buttons stop the click reaching it.
          onClick={(event) => event.stopPropagation()}
        >
          <Button variant="outline" size="sm" disabled={anyPending} onClick={() => startEdit(row)}>
            Edit
          </Button>
          <AlertDialog
            open={deleting?.id === row.id}
            onOpenChange={(open) => {
              if (!open) setDeleting(null)
            }}
          >
            <AlertDialogTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                disabled={anyPending}
                onClick={() => setDeleting(row)}
              >
                <Trash2 className="size-4" aria-hidden="true" />
                Delete
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete this service?</AlertDialogTitle>
                <AlertDialogDescription>
                  {deleting?.name} will be removed from the control plane. This cannot be
                  undone.
                  {dependents.data && dependents.data.routes.length > 0 ? (
                    <span className="mt-2 block font-medium text-destructive">
                      {dependents.data.routes.length}{' '}
                      {dependents.data.routes.length === 1 ? 'route' : 'routes'} reference this
                      service:{' '}
                      {dependents.data.routes
                        .slice(0, 3)
                        .map((route) => route.key)
                        .join(', ')}
                      {dependents.data.routes.length > 3 ? ', …' : ''}. They will be left
                      pointing at a service that no longer exists.
                    </span>
                  ) : null}
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction
                  onClick={() => {
                    if (deleting) {
                      mutations.remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) })
                    }
                  }}
                >
                  Delete service
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Services"
        description="The backends that routes forward to."
        actions={
          <Button onClick={startCreate} disabled={anyPending}>
            <Plus className="size-4" aria-hidden="true" />
            New service
          </Button>
        }
      />

      {services.isError ? (
        <ErrorState
          title="Could not load services"
          message={services.error instanceof Error ? services.error.message : String(services.error)}
        />
      ) : null}

      {editing ? (
        <section className="mb-6 rounded-lg border p-5">
          <h2 className="font-heading mb-4 text-lg font-semibold">
            {editingId ? 'Edit service' : 'New service'}
          </h2>
          <ServiceForm
            initial={editing}
            submitLabel={editingId ? 'Save changes' : 'Create service'}
            pending={anyPending}
            error={editingId ? mutations.update.error : mutations.create.error}
            onSubmit={submit}
            onCancel={() => {
              setEditing(null)
              setEditingId(null)
            }}
          />
        </section>
      ) : null}

      {rows.length === 0 && !services.isPending && !services.isError ? (
        <EmptyState
          title="No services yet"
          description="A service is the backend a route forwards to. Create one before adding routes."
          action={
            <Button onClick={startCreate}>
              <Plus className="size-4" aria-hidden="true" />
              New service
            </Button>
          }
        />
      ) : (
        <DataTable
          caption="Services known to the control plane"
          columns={columns}
          rows={rows}
          rowKey={(row) => row.id}
          totalCount={services.data?.totalCount ?? 0}
          page={services.data?.page ?? page}
          pageSize={services.data?.pageSize ?? pageSize}
          onPageChange={(next) => updateParams({ [PAGE]: next })}
          pageSizeOptions={PAGE_SIZE_OPTIONS}
          onPageSizeChange={(next) => updateParams({ [PAGE]: 1, [PAGE_SIZE]: next })}
          isLoading={services.isPending}
          onRowClick={(row) => navigate(`/services/${row.id}`)}
        />
      )}

      {mutations.remove.isError ? (
        <div className="mt-4">
          <ErrorState
            title="Could not delete the service"
            message={
              mutations.remove.error instanceof Error
                ? mutations.remove.error.message
                : String(mutations.remove.error)
            }
          />
        </div>
      ) : null}
    </>
  )
}
