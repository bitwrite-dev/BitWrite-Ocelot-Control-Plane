import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type { DeliveryMetricsResponse, ReconcileResponse } from '@/api'

/**
 * Monitoring: whether configuration is reaching gateways, and what happens when it
 * is asked to.
 *
 * This page reports *delivery*, not traffic. Nothing in the system counts the
 * requests a gateway serves or times them, so there is no request rate, latency or
 * error rate to show, and the page says so where those figures would be rather than
 * drawing a flat line that reads as a healthy gateway. A gateway serving traffic
 * wrongly is invisible here — which is the one thing a reader should not have to
 * guess about a monitoring page.
 */

/** The ranges offered. Named rather than free-form, because "the last hour" is the question. */
export const RANGES = [
  { label: 'Last hour', hours: 1 },
  { label: 'Last 24 hours', hours: 24 },
  { label: 'Last 7 days', hours: 24 * 7 },
  { label: 'Last 30 days', hours: 24 * 30 },
] as const

export type RangeHours = (typeof RANGES)[number]['hours']

export const defaultRange: RangeHours = 24

/** The window ending now, as the API takes it. */
export function rangeSince(hours: RangeHours, now: Date = new Date()): string {
  return new Date(now.getTime() - hours * 60 * 60 * 1000).toISOString()
}

export function useDeliveryMetrics(hours: RangeHours) {
  const api = useApi()

  return useQuery({
    // The range is in the key, so switching it cannot show the previous range's
    // numbers under the new heading while the request is in flight.
    queryKey: ['monitoring', 'delivery', hours],
    queryFn: ({ signal }) =>
      api.resources.runtime.deliveryMetrics({ from: rangeSince(hours), signal }),
    // Delivery outcomes do not change until someone publishes something, so a short
    // staleness window saves a round trip without showing anything out of date.
    staleTime: 15_000,
    placeholderData: (previous) => previous,
  })
}

export function useGateways() {
  const api = useApi()

  return useQuery({
    queryKey: ['monitoring', 'gateways'],
    queryFn: ({ signal }) => api.resources.runtime.gateways({ signal }),
    // Status and heartbeat age move on their own, so this is refetched on focus
    // rather than left to go stale on a timer the reader cannot see.
    staleTime: 10_000,
  })
}

/**
 * Asks a gateway to bring its configuration up to a version.
 *
 * Invalidates both the gateway list and the delivery report: a reconcile produces
 * a new attempt, and leaving the numbers as they were would report the outcome of
 * the action the reader just took as if they had not taken it.
 */
export function useReconcile() {
  const api = useApi()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: { gatewayId: string; targetVersion: number; initiatedBy: string }) =>
      api.resources.runtime.reconcile(input) as Promise<ReconcileResponse>,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['monitoring'] })
    },
  })
}

/**
 * What one delivery report says, in the order a reader needs it.
 *
 * Nulls are kept as nulls all the way to the screen. A report with no attempts has
 * no success rate, and showing 100% — or 0% — would state a proportion nobody
 * measured.
 */
export interface DeliverySummary {
  attempts: number
  successful: number
  failed: number
  /** Null when nothing was attempted. */
  successRate: number | null
  gatewayCount: number
  distinctFailures: number
}

export function summarise(metrics: DeliveryMetricsResponse | undefined): DeliverySummary {
  return {
    attempts: metrics?.totalAttempts ?? 0,
    successful: metrics?.successful ?? 0,
    failed: metrics?.failed ?? 0,
    successRate: metrics?.successRate ?? null,
    gatewayCount: metrics?.gateways.length ?? 0,
    distinctFailures: metrics?.errors.length ?? 0,
  }
}

/**
 * How many attempts the report actually examined.
 *
 * Shown beside the totals because the API reads a bounded tail of the attempt log:
 * a report over a long range can cover fewer attempts than the range holds, and
 * that number is the only honest way to say so. It does not say whether more
 * exist — the API does not report the length of the log — so it is presented as
 * what was read, never as the size of the range.
 */
export function examined(metrics: DeliveryMetricsResponse | undefined): number {
  return metrics?.consideredAttempts ?? 0
}

/**
 * How long ago a heartbeat arrived, in words.
 *
 * Null when the gateway has never reported, which is different from having
 * reported long ago: one has no data, the other has stale data.
 */
export function heartbeatAge(
  lastHeartbeat: string | null,
  now: Date = new Date(),
): { text: string; stale: boolean } | null {
  if (!lastHeartbeat) return null

  const at = new Date(lastHeartbeat)
  if (Number.isNaN(at.getTime())) return null

  const seconds = Math.max(0, Math.round((now.getTime() - at.getTime()) / 1000))
  const stale = seconds > 90

  return { text: describeAge(seconds), stale }
}

function describeAge(seconds: number): string {
  if (seconds < 60) return `${seconds}s ago`
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`
  return `${Math.floor(seconds / 86400)}d ago`
}