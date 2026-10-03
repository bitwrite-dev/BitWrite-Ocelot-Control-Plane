import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'

import { StatusBadge } from '@/components/status-badge'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'

import {
  defaultRange,
  examined,
  heartbeatAge,
  RANGES,
  summarise,
  useDeliveryMetrics,
  useGateways,
  useReconcile,
  type RangeHours,
} from './queries'

/**
 * Monitoring.
 *
 * Two questions, kept apart on purpose. *Is configuration reaching gateways* is
 * answered from what gateways reported when they applied a snapshot. *How much
 * traffic are they serving* cannot be answered at all: nothing in the system counts
 * requests or times them. Rather than draw charts that are flat because nothing was
 * measured, the page names the gap where those figures would be — a monitoring page
 * that implies it is watching traffic when it is not is worse than one that admits
 * it cannot.
 */
export function MonitoringPage() {
  const [range, setRange] = useState<RangeHours>(defaultRange)

  const metrics = useDeliveryMetrics(range)
  const gateways = useGateways()
  const reconcile = useReconcile()

  const summary = summarise(metrics.data)
  const read = examined(metrics.data)
  const rows = metrics.data?.gateways ?? []
  const failures = metrics.data?.errors ?? []
  const instances = gateways.data?.gateways ?? []

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold">Monitoring</h1>
        <p className="text-sm text-muted-foreground">
          Whether configuration is reaching gateways, and what happens when it is asked to.
          Every figure below comes from what gateways reported when they applied a snapshot.
        </p>
      </header>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Configuration delivery</CardTitle>
          <CardDescription>
            Attempts gateways made to apply a snapshot, and how they fared.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-2">
            <Label htmlFor="range" className="text-sm">
              Range
            </Label>
            <Select value={String(range)} onValueChange={(value) => setRange(Number(value) as RangeHours)}>
              <SelectTrigger id="range" className="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {RANGES.map((option) => (
                  <SelectItem key={option.hours} value={String(option.hours)}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {metrics.isPending ? (
            <div className="grid gap-4 sm:grid-cols-4">
              {[0, 1, 2, 3].map((index) => (
                <Skeleton key={index} className="h-20" />
              ))}
            </div>
          ) : metrics.isError ? (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Delivery metrics could not be read</AlertTitle>
              <AlertDescription>{errorText(metrics.error)}</AlertDescription>
            </Alert>
          ) : summary.attempts === 0 ? (
            <Alert>
              <AlertTitle>No attempts in this range</AlertTitle>
              <AlertDescription>
                Nothing was asked of a gateway between {formatDate(metrics.data?.from)} and{' '}
                {formatDate(metrics.data?.to)}. That is not a success rate — there is no rate to
                report until something is attempted.
              </AlertDescription>
            </Alert>
          ) : (
            <>
              <div className="grid gap-4 sm:grid-cols-4">
                <Figure label="Attempts" value={String(summary.attempts)} />
                <Figure label="Succeeded" value={String(summary.successful)} />
                <Figure label="Failed" value={String(summary.failed)} tone={summary.failed > 0 ? 'bad' : 'good'} />
                <Figure
                  label="Success rate"
                  value={summary.successRate === null ? '—' : percent(summary.successRate)}
                />
              </div>
              <p className="text-xs text-muted-foreground">
                {read} attempt{read === 1 ? '' : 's'} examined, across {summary.gatewayCount} gateway
                {summary.gatewayCount === 1 ? '' : 's'}.
              </p>
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Traffic</CardTitle>
          <CardDescription>Request rate, latency and error rate, per route.</CardDescription>
        </CardHeader>
        <CardContent>
          {/* Named rather than left out. A reader who came to a monitoring page for
              latency needs to be told it is not here and why, not to find an absence
              and assume the number was zero. */}
          <Alert>
            <AlertTitle>Not measured</AlertTitle>
            <AlertDescription>
              No gateway counts the requests it serves or times them, so there is no request rate,
              latency or error rate to report — and nothing here stands in for one. What is shown
              above is configuration delivery, which is a different question: whether the
              configuration reached the gateway, not whether the gateway is serving correctly.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>

      {failures.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Failures</CardTitle>
            <CardDescription>
              Grouped by cause, so one fault is not counted as many.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <ul className="space-y-2">
              {failures.map((failure) => (
                <li key={failure.message} className="flex items-start justify-between gap-4 text-sm">
                  <span className="min-w-0 break-words">{failure.message}</span>
                  <span className="shrink-0 text-muted-foreground">
                    ×{failure.occurrences} · {formatDate(failure.lastSeenAt)}
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Delivery by gateway</CardTitle>
          <CardDescription>What each gateway reported when it applied a configuration.</CardDescription>
        </CardHeader>
        <CardContent>
          {metrics.isPending ? (
            <Skeleton className="h-40" />
          ) : rows.length === 0 ? (
            <p className="text-sm text-muted-foreground">No gateway has reported an attempt in this range.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Gateway</TableHead>
                  <TableHead className="text-right">Attempts</TableHead>
                  <TableHead className="text-right">Failed</TableHead>
                  <TableHead className="text-right">Rate</TableHead>
                  <TableHead>Last attempt</TableHead>
                  <TableHead>Reason</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.gatewayId}>
                    <TableCell className="font-mono text-xs">{short(row.gatewayId)}</TableCell>
                    <TableCell className="text-right">{row.attempts}</TableCell>
                    <TableCell className="text-right">{row.failed}</TableCell>
                    <TableCell className="text-right">
                      {row.successRate === null ? '—' : percent(row.successRate)}
                    </TableCell>
                    <TableCell className="text-muted-foreground">{formatDate(row.lastAttemptAt)}</TableCell>
                    <TableCell className="max-w-md text-xs text-muted-foreground">
                      {row.recentErrors[0] ?? '—'}
                      {row.recentErrors.length > 1 && ` (+${row.recentErrors.length - 1} more)`}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Gateway state</CardTitle>
          <CardDescription>
            Status and the configuration version each gateway is running.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {gateways.isPending ? (
            <Skeleton className="h-40" />
          ) : gateways.isError ? (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Gateway state could not be read</AlertTitle>
              <AlertDescription>{errorText(gateways.error)}</AlertDescription>
            </Alert>
          ) : instances.length === 0 ? (
            <p className="text-sm text-muted-foreground">No gateway is registered.</p>
          ) : (
            <ul className="space-y-3">
              {instances.map((instance) => {
                const beat = heartbeatAge(instance.lastHeartbeat)
                return (
                  <li
                    key={instance.gatewayId}
                    className="flex flex-wrap items-center justify-between gap-3 text-sm"
                  >
                    <span className="font-mono text-xs">{short(instance.gatewayId)}</span>
                    <StatusBadge status={instance.status} />
                    <span className="text-muted-foreground">
                      running v{instance.currentVersion ?? '—'}
                      {instance.targetVersion !== null && ` → v${instance.targetVersion}`}
                    </span>
                    {/* A gateway that has stopped reporting is the thing this page
                        exists to make visible, so it is said in words rather than
                        left as a timestamp to interpret. */}
                    <span className={beat?.stale ? 'text-destructive' : 'text-muted-foreground'}>
                      {beat === null
                        ? 'never reported'
                        : beat.stale
                          ? `${beat.text}, not reporting`
                          : `reported ${beat.text}`}
                    </span>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={reconcile.isPending || instance.targetVersion === null}
                      onClick={() =>
                        reconcile.mutate({
                          gatewayId: instance.gatewayId,
                          targetVersion: instance.targetVersion ?? 0,
                          initiatedBy: 'dashboard',
                        })
                      }
                    >
                      Reconcile
                    </Button>
                  </li>
                )
              })}
            </ul>
          )}

          {reconcile.isError && (
            <Alert variant="destructive" className="mt-4">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Reconcile failed</AlertTitle>
              <AlertDescription>{errorText(reconcile.error)}</AlertDescription>
            </Alert>
          )}
          {reconcile.isSuccess && (
            <Alert className="mt-4">
              <AlertTitle>
                Reconcile {reconcile.data.success ? 'accepted' : 'requested'}
              </AlertTitle>
              <AlertDescription>
                {short(reconcile.data.gatewayId)} was asked to bring its configuration to v
                {reconcile.data.targetVersion}.
                {!reconcile.data.success && reconcile.data.errorMessage
                  ? ` It reported: ${reconcile.data.errorMessage}`
                  : ''}
              </AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function Figure({
  label,
  value,
  tone,
}: {
  label: string
  value: string
  tone?: 'good' | 'bad'
}) {
  return (
    <div className="rounded-md border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div
        className={
          tone === 'bad'
            ? 'mt-1 text-2xl font-semibold text-destructive'
            : tone === 'good'
              ? 'mt-1 text-2xl font-semibold'
              : 'mt-1 text-2xl font-semibold'
        }
      >
        {value}
      </div>
    </div>
  )
}

function percent(rate: number): string {
  return `${(rate * 100).toFixed(rate === 1 || rate === 0 ? 0 : 1)}%`
}

function short(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id
}

/**
 * A timestamp, or the word "never".
 *
 * The value is split on the `T` and the date left in UTC, because these are UTC
 * timestamps and rendering them in the browser's zone would show a delivery as
 * having happened on a different day than it did.
 */
function formatDate(value: string | undefined | null): string {
  if (!value) return 'never'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return 'unknown'
  return `${parsed.toISOString().slice(0, 10)} ${parsed.toISOString().slice(11, 16)}Z`
}

function errorText(error: unknown): string {
  if (error && typeof error === 'object' && 'message' in error) {
    return String((error as { message: unknown }).message)
  }
  return 'The request failed.'
}