import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ClipboardCopy, Check, Trash2 } from 'lucide-react'

import { ApiError } from '@/api'
import { PageHeader } from '@/components/app-layout'
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { EmptyState, ErrorState, LoadingState } from '@/components/page-state'
import { EnabledBadge } from '@/components/status-badge'
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  toRouteError,
  useRoute,
  useRouteEffective,
  useRouteHistory,
  useRouteMutations,
  useServiceOptions,
  useServiceRoutes,
} from './queries'
import { JsonHighlight, copyToClipboard } from './json-highlight'

/** One labelled value in the configuration view. */
function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid gap-1 border-b py-2.5 last:border-b-0 sm:grid-cols-[190px_1fr] sm:gap-4">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  )
}

function Code({ children }: { children: React.ReactNode }) {
  return <span className="font-mono text-xs break-all">{children}</span>
}

/** Shown where an option is not configured, so absence is explicit. */
function Absent() {
  return <span className="text-muted-foreground">Not configured</span>
}

export function RouteDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [tab, setTab] = useState('configuration')
  const [copied, setCopied] = useState(false)

  const route = useRoute(id)
  const effective = useRouteEffective(id, tab === 'effective')
  const history = useRouteHistory(id, tab === 'history')
  const services = useServiceOptions()
  const { enable, disable, remove } = useRouteMutations()

  // Only fetched for the section that shows them, to keep the page cheap.
  const related = useServiceRoutes(route.data?.serviceId, tab === 'related')

  if (route.isPending) return <LoadingState label="Loading route" />

  if (route.isError) {
    const error = toRouteError(route.error)
    // A missing route is a normal outcome, not a failure worth alarming about.
    if (isNotFound(route.error)) {
      return (
        <>
          <PageHeader title="Route not found" />
          <EmptyState
            title="No such route"
            description={
              id
                ? `No route with id ${id} exists. It may have been deleted.`
                : 'No route was requested.'
            }
            action={
              <Button asChild>
                <Link to="/routes">Back to routes</Link>
              </Button>
            }
          />
        </>
      )
    }

    return (
      <>
        <PageHeader title="Route" />
        <ErrorState {...error} />
      </>
    )
  }

  const data = route.data
  const serviceName =
    services.data?.services.find((service) => service.id === data.serviceId)?.name ??
    data.serviceId
  const anyActionPending = enable.isPending || disable.isPending || remove.isPending

  return (
    <>
      <PageHeader
        title={data.key}
        description={`${data.method} ${data.upstreamPath}`}
        actions={
          <>
            <Button asChild>
              <Link to={`/routes/${data.id}/edit`}>Edit</Link>
            </Button>

            {data.isEnabled ? (
              <Button
                variant="outline"
                disabled={anyActionPending}
                onClick={() => disable.mutate(data.id)}
              >
                {disable.isPending ? 'Disabling…' : 'Disable'}
              </Button>
            ) : (
              <Button
                variant="outline"
                disabled={anyActionPending}
                onClick={() => enable.mutate(data.id)}
              >
                {enable.isPending ? 'Enabling…' : 'Enable'}
              </Button>
            )}

            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="destructive" disabled={anyActionPending}>
                  <Trash2 className="size-4" aria-hidden="true" />
                  Delete
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Delete this route?</AlertDialogTitle>
                  <AlertDialogDescription>
                    {data.key} will be removed from the control plane. This cannot be undone.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={() =>
                      remove.mutate(data.id, {
                        onSuccess: () => navigate('/routes', { replace: true }),
                      })
                    }
                  >
                    Delete route
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <EnabledBadge isEnabled={data.isEnabled} />
        <span className="text-sm text-muted-foreground">
          Belongs to{' '}
          <span className="text-foreground">{serviceName}</span>
        </span>
        {enable.error || disable.error || remove.error ? (
          <ErrorState
            {...toRouteError(enable.error ?? disable.error ?? remove.error)}
          />
        ) : null}
      </div>

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="configuration">Configuration</TabsTrigger>
          <TabsTrigger value="effective">Effective config</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
          <TabsTrigger value="related">Related routes</TabsTrigger>
        </TabsList>

        <TabsContent value="configuration">
          <dl className="rounded-lg border px-4">
            <Row label="Key">
              <Code>{data.key}</Code>
            </Row>
            <Row label="Id">
              <Code>{data.id}</Code>
            </Row>
            <Row label="Method">{data.method}</Row>
            <Row label="Upstream path">
              <Code>{data.upstreamPath}</Code>
            </Row>
            <Row label="Host">{data.host ? <Code>{data.host}</Code> : <Absent />}</Row>
            <Row label="Service">{serviceName}</Row>
            <Row label="Downstream targets">
              {data.downstreamTargets.length > 0 ? (
                <ul className="space-y-1">
                  {data.downstreamTargets.map((target) => (
                    <li key={`${target.host}:${target.port}${target.path}`}>
                      <Code>
                        {target.scheme}://{target.host}:{target.port}
                        {target.path}
                      </Code>
                    </li>
                  ))}
                </ul>
              ) : (
                <Absent />
              )}
            </Row>
            <Row label="Authentication">
              {data.authenticationOptions?.allowedScopes?.length ? (
                <ul className="flex flex-wrap gap-1.5">
                  {data.authenticationOptions.allowedScopes.map((scope) => (
                    <li
                      key={scope}
                      className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs"
                    >
                      {scope}
                    </li>
                  ))}
                </ul>
              ) : (
                <Absent />
              )}
            </Row>
            <Row label="Rate limiting">
              {data.rateLimitOptions?.enableRateLimiting ? (
                <Code>
                  {data.rateLimitOptions.limit} / {data.rateLimitOptions.period}
                </Code>
              ) : (
                <Absent />
              )}
            </Row>
            <Row label="QoS timeout">
              {data.qoSOptions ? (
                <Code>{data.qoSOptions.timeoutSeconds}s</Code>
              ) : (
                <Absent />
              )}
            </Row>
            <Row label="Circuit breaker timeout">
              {data.qoSOptions?.circuitBreakerTimeoutSeconds != null ? (
                <Code>{data.qoSOptions.circuitBreakerTimeoutSeconds}s</Code>
              ) : (
                <Absent />
              )}
            </Row>
            <Row label="Cache TTL">
              {data.cacheOptions ? <Code>{data.cacheOptions.ttlSeconds}s</Code> : <Absent />}
            </Row>
            <Row label="Load balancing">
              {data.loadBalancerOptions ? (
                <Code>{data.loadBalancerOptions.algorithm}</Code>
              ) : (
                <Absent />
              )}
            </Row>
            <Row label="Created">
              <time dateTime={data.createdAt}>{formatDate(data.createdAt)}</time>
            </Row>
            <Row label="Last updated">
              <time dateTime={data.updatedAt}>{formatDate(data.updatedAt)}</time>
            </Row>
          </dl>
        </TabsContent>

        <TabsContent value="effective">
          {effective.isPending ? (
            <LoadingState label="Building effective configuration" />
          ) : effective.isError ? (
            <ErrorState {...toRouteError(effective.error)} />
          ) : (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <p className="text-sm text-muted-foreground">
                  The Ocelot configuration this route produces.
                </p>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={async () => {
                    const ok = await copyToClipboard(effective.data.ocelotJson)
                    if (ok) {
                      setCopied(true)
                      setTimeout(() => setCopied(false), 2000)
                    }
                  }}
                >
                  {copied ? (
                    <Check className="size-4" aria-hidden="true" />
                  ) : (
                    <ClipboardCopy className="size-4" aria-hidden="true" />
                  )}
                  {copied ? 'Copied' : 'Copy'}
                </Button>
              </div>
              <JsonHighlight payload={effective.data.ocelotJson} />
            </div>
          )}
        </TabsContent>

        <TabsContent value="history">
          {history.isPending ? (
            <LoadingState label="Loading history" />
          ) : history.isError ? (
            <ErrorState {...toRouteError(history.error)} />
          ) : history.data.history.length === 0 ? (
            <EmptyState
              title="No history yet"
              description="Changes to this route will be listed here."
            />
          ) : (
            <SimpleTable
              caption="Change history for this route"
              columns={[
                {
                  key: 'timestamp',
                  header: 'When',
                  cell: (item) => (
                    <time dateTime={item.timestamp}>{formatDate(item.timestamp)}</time>
                  ),
                },
                { key: 'action', header: 'Action', cell: (item) => item.action },
                { key: 'changedBy', header: 'By', cell: (item) => item.changedBy ?? <Absent /> },
                { key: 'details', header: 'Details', cell: (item) => item.details ?? <Absent /> },
              ]}
              rows={history.data.history}
              rowKey={(item) => `${item.timestamp}-${item.action}`}
            />
          )}
        </TabsContent>

        <TabsContent value="related">
          {related.isPending ? (
            <LoadingState label="Loading related routes" />
          ) : related.isError ? (
            <ErrorState {...toRouteError(related.error)} />
          ) : related.data.routes.length === 0 ? (
            <EmptyState
              title="No related routes"
              description="This is the only route on its service."
            />
          ) : (
            <SimpleTable
              caption="Other routes on the same service"
              columns={[
                { key: 'key', header: 'Key', cell: (row) => <Code>{row.key}</Code> },
                { key: 'method', header: 'Method', cell: (row) => row.method },
                {
                  key: 'upstreamPath',
                  header: 'Upstream path',
                  cell: (row) => <Code>{row.upstreamPath}</Code>,
                },
                {
                  key: 'status',
                  header: 'Status',
                  cell: (row) => <EnabledBadge isEnabled={row.isEnabled} />,
                },
              ]}
              rows={related.data.routes}
              rowKey={(row) => row.id}
              onRowClick={(row) => navigate(`/routes/${row.id}`)}
            />
          )}
        </TabsContent>
      </Tabs>
    </>
  )
}

/**
 * Read-only table for endpoints that return a plain list.
 *
 * The shared `DataTable` is server-paginated and needs a total and a page
 * handler; neither the history nor the related-routes endpoint paginates, so
 * faking those props would misdescribe the data.
 */
function SimpleTable<T>({
  caption,
  columns,
  rows,
  rowKey,
  onRowClick,
}: {
  caption: string
  columns: { key: string; header: string; cell: (row: T) => React.ReactNode }[]
  rows: T[]
  rowKey: (row: T) => string
  onRowClick?: (row: T) => void
}) {
  return (
    <div className="rounded-lg border">
      <Table>
        <TableCaption className="sr-only">{caption}</TableCaption>
        <TableHeader>
          <TableRow>
            {columns.map((column) => (
              <TableHead key={column.key}>{column.header}</TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow
              key={rowKey(row)}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              className={onRowClick ? 'cursor-pointer' : undefined}
            >
              {columns.map((column) => (
                <TableCell key={column.key}>{column.cell(row)}</TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

function formatDate(value: string): string {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}

/** A missing route is an expected outcome, not a failure to report as an error. */
function isNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.isNotFound
}
