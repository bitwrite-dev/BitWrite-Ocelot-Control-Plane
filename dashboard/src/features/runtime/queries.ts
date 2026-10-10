import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type {
  RuntimeGatewaysResponse,
  RuntimeStatusResponse,
  ReconcileResponse,
  DeliveryMetricsResponse,
} from '@/api'

/**
 * Runtime: gateway heartbeats, reconciliation, and delivery metrics.
 */

// Type assertions to silence unused import warnings
const _t1: RuntimeGatewaysResponse = null as any
const _t2: RuntimeStatusResponse = null as any
const _t3: ReconcileResponse = null as any
const _t4: DeliveryMetricsResponse = null as any
void _t1; void _t2; void _t3; void _t4

export const runtimeKeys = {
  gateways: () => ['runtime', 'gateways'] as const,
  gateway: (id: string) => ['runtime', 'gateway', id] as const,
  metrics: (from?: string, to?: string) =>
    ['runtime', 'metrics', from, to] as const,
}

export function useRuntimeGateways() {
  const api = useApi()

  return useQuery<RuntimeGatewaysResponse>({
    queryKey: runtimeKeys.gateways(),
    queryFn: ({ signal }) => api.resources.runtime.gateways({ signal }),
  })
}

export function useRuntimeGateway(id: string, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: ['runtime', 'gateway', id],
    queryFn: ({ signal }) => api.resources.runtime.gateway(id, { signal }),
    enabled: enabled && !!id,
  })
}

export function useDeliveryMetrics(from?: string, to?: string) {
  const api = useApi()

  return useQuery({
    queryKey: runtimeKeys.metrics(from, to),
    queryFn: ({ signal }) => api.resources.runtime.deliveryMetrics({ from, to, signal }),
  })
}

export function useReconcile() {
  const api = useApi()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (gatewayId: string) =>
      api.resources.runtime.reconcile({ gatewayId }, undefined),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['runtime', 'gateways'] })
      queryClient.invalidateQueries({ queryKey: ['runtime', 'metrics'] })
    },
  })
}

/** Format a timestamp for display. */
export function formatTimestamp(value: string | null): string {
  if (!value) return '—'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}

/** Shorten a hash for display. */
export function shortHash(hash: string): string {
  if (!hash) return '—'
  return hash.length <= 14 ? hash : `${hash.slice(0, 12)}…`
}

/** Gateway status badge tone. */
export function gatewayStatusTone(status: string): { tone: 'ok' | 'bad' | 'warn' | 'mute' } {
  const s = status.toLowerCase()
  if (s.includes('active') || s.includes('healthy') || s.includes('connected')) return { tone: 'ok' }
  if (s.includes('error') || s.includes('fail') || s.includes('disconnected')) return { tone: 'bad' }
  if (s.includes('pending') || s.includes('syncing') || s.includes('reconciling')) return { tone: 'warn' }
  return { tone: 'mute' }
}