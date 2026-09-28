import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type {
  CompleteFirstRunRequest,
  SystemSettingsResponse,
  UpdateSystemSettingsRequest,
} from '@/api'

/**
 * Query hooks for the Settings page.
 *
 * There is one row of settings and it is read whole, so there is no list, no
 * detail key and no paging. `isInitialised` is the field the page turns on: it
 * is false exactly when no Ocelot version has been chosen, which is the
 * first-run state.
 */

export const settingsKeys = {
  detail: () => ['settings', 'detail'] as const,
}

export function useSystemSettings() {
  const api = useApi()

  return useQuery({
    queryKey: settingsKeys.detail(),
    queryFn: ({ signal }) => api.resources.settings.get({ signal }),
  })
}

/**
 * First-run completion and the ordinary update.
 *
 * `completeFirstRun` is separate from `update` rather than folded into it,
 * because it is the only request that can set the Ocelot version. Folding the
 * two would make the permanent field look editable.
 */
export function useSettingsMutations() {
  const api = useApi()
  const queryClient = useQueryClient()

  const invalidate = () => queryClient.invalidateQueries({ queryKey: settingsKeys.detail() })

  return {
    completeFirstRun: useMutation({
      mutationFn: (body: CompleteFirstRunRequest) =>
        api.resources.settings.completeFirstRun(body),
      onSuccess: invalidate,
    }),
    update: useMutation({
      mutationFn: (body: UpdateSystemSettingsRequest) => api.resources.settings.update(body),
      onSuccess: invalidate,
    }),
  }
}

/** The editable state of the operational settings. */
export interface SettingsDraft {
  pollIntervalSeconds: string
  auditLogRetentionDays: string
  snapshotRetentionCount: string
}

export const emptySettingsDraft = (): SettingsDraft => ({
  pollIntervalSeconds: '',
  auditLogRetentionDays: '',
  snapshotRetentionCount: '',
})

export function draftFromSettings(settings: SystemSettingsResponse): SettingsDraft {
  return {
    pollIntervalSeconds: String(settings.pollIntervalSeconds),
    auditLogRetentionDays: String(settings.auditLogRetentionDays),
    snapshotRetentionCount: String(settings.snapshotRetentionCount),
  }
}

/**
 * The rules for the editable settings, checked here rather than only on submit.
 *
 * The bounds mirror the API's `Range` and the domain's own checks. Duplicating
 * them is a risk, but an operator who only finds out after a round trip has
 * already lost the reason.
 */
export function settingsErrors(draft: SettingsDraft): Record<string, string> {
  const errors: Record<string, string> = {}

  const poll = Number(draft.pollIntervalSeconds)
  if (draft.pollIntervalSeconds.trim() === '' || !Number.isInteger(poll) || poll < 5 || poll > 3600) {
    // Below five seconds a gateway spends its time asking rather than serving.
    errors.pollIntervalSeconds = 'Poll interval must be between 5 and 3600 seconds'
  }

  const retention = Number(draft.auditLogRetentionDays)
  if (
    draft.auditLogRetentionDays.trim() === '' ||
    !Number.isInteger(retention) ||
    retention < 0 ||
    retention > 3650
  ) {
    errors.auditLogRetentionDays =
      'Audit retention must be 0 (keep forever) or between 1 and 3650 days'
  }

  const snapshots = Number(draft.snapshotRetentionCount)
  if (
    draft.snapshotRetentionCount.trim() === '' ||
    !Number.isInteger(snapshots) ||
    snapshots < 0
  ) {
    errors.snapshotRetentionCount = 'Snapshot retention must be 0 (keep all) or a positive count'
  }

  return errors
}

/**
 * The request body, with a blank field sent as null rather than dropped.
 *
 * A blank number sent as `undefined` disappears from the JSON entirely, which
 * the API reads as "leave this alone" — so a field the operator deliberately
 * cleared would silently keep its old value.
 */
export function toUpdateRequest(draft: SettingsDraft): UpdateSystemSettingsRequest {
  return {
    pollIntervalSeconds: toNumberOrNull(draft.pollIntervalSeconds),
    auditLogRetentionDays: toNumberOrNull(draft.auditLogRetentionDays),
    snapshotRetentionCount: toNumberOrNull(draft.snapshotRetentionCount),
  }
}

function toNumberOrNull(value: string): number | null {
  const trimmed = value.trim()
  if (trimmed === '') return null
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}
