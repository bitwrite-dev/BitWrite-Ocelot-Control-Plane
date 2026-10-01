import { useEffect, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { CircleAlert, Info, Lock } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import type { SystemSettingsResponse } from '@/api'
import { isPermissionError } from '@/lib/api-error'

import {
  draftFromSettings,
  settingsErrors,
  toUpdateRequest,
  useSettingsMutations,
  useSystemSettings,
  type SettingsDraft,
} from './queries'

/**
 * System settings: the Ocelot version, and the operational configuration the
 * control plane owns.
 *
 * The version is the reason this page is not an ordinary form. It is chosen once
 * and has no update path, so it is presented as a fact rather than a field — and
 * before it is chosen, this page is the first-run screen.
 */
export function SettingsPage() {
  const { data, isPending, isError, error: loadError, refetch } = useSystemSettings()
  const { update, completeFirstRun } = useSettingsMutations()
  const location = useLocation()

  // The guard sends a reason along with the redirect. Reading it here means the
  // operator is told why they are looking at setup rather than being left to
  // wonder which screen they asked for.
  const arrivalReason =
    (location.state as { reason?: string } | null)?.reason ?? undefined

  if (isPending) return <SettingsSkeleton />
  if (isError || !data) {
    return (
      <SettingsError
        error={loadError ?? new Error('No settings were returned')}
        onRetry={() => {
          refetch()
        }}
      />
    )
  }

  return data.isInitialised ? (
    <ConfiguredSettings settings={data} onSave={(draft) => update.mutate(toUpdateRequest(draft))} isSaving={update.isPending} error={update.error} />
  ) : (
    <FirstRunSettings
      settings={data}
      onComplete={(body) => completeFirstRun.mutate(body)}
      isSubmitting={completeFirstRun.isPending}
      error={completeFirstRun.error}
      arrivalReason={arrivalReason}
    />
  )
}

/**
 * First run: the version has to be chosen before anything else can happen.
 *
 * There is no way past this screen and no way to skip it, because the generated
 * configuration has no shape without a version. The API refuses to guess, so the
 * page says why rather than offering a default that would hide the decision.
 */
function FirstRunSettings({
  settings,
  onComplete,
  isSubmitting,
  error,
  arrivalReason,
}: {
  settings: SystemSettingsResponse
  onComplete: (body: {
    ocelotVersion: string
    initiatedBy?: string
    pollIntervalSeconds?: number
    auditLogRetentionDays?: number
    snapshotRetentionCount?: number
  }) => void
  isSubmitting: boolean
  error: unknown
  arrivalReason?: string
}) {
  const [version, setVersion] = useState('')
  // With no session yet, this is the only name the audit trail can get, and the
  // choice it will record cannot be undone.
  const [operator, setOperator] = useState('')
  const [draft, setDraft] = useState<SettingsDraft>(() => draftFromSettings(settings))
  const errors = settingsErrors(draft)

  // The default is the only offered version, but pre-selecting it would make the
  // choice look like it had already been made. It is left unselected.
  const available = settings.availableOcelotVersions
  const versionMissing = version.trim() === ''

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    if (versionMissing || Object.keys(errors).length > 0) return

    onComplete({
      ocelotVersion: version.trim(),
      initiatedBy: operator.trim() || undefined,
      pollIntervalSeconds: Number(draft.pollIntervalSeconds),
      auditLogRetentionDays: Number(draft.auditLogRetentionDays),
      snapshotRetentionCount: Number(draft.snapshotRetentionCount),
    })
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold">Set up the control plane</h1>
        <p className="text-sm text-muted-foreground">
          Choose the Ocelot version this installation targets. It cannot be changed
          afterwards, because it determines the shape of every configuration
          generated from here on.
        </p>
      </header>

      {arrivalReason ? (
        <Alert>
          <Info aria-hidden="true" className="size-4" />
          <AlertTitle>Setup comes first</AlertTitle>
          <AlertDescription>{arrivalReason}</AlertDescription>
        </Alert>
      ) : null}

      <Alert>
        <Info aria-hidden="true" className="size-4" />
        <AlertTitle>This choice is permanent</AlertTitle>
        <AlertDescription>
          Every published snapshot is immutable and hashed against this version. Changing
          it later would leave those snapshots wrong for the new version, and a route that
          cannot be expressed in a target version has no sensible fallback.
        </AlertDescription>
      </Alert>

      <form onSubmit={submit} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Ocelot version</CardTitle>
            <CardDescription>
              Only versions whose configuration shapes are established are listed. Offering
              one that is not would generate a file no gateway could read, and the failure
              would not appear until a gateway refused to start.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <Label htmlFor="ocelot-version">Version</Label>
            <Select value={version} onValueChange={setVersion}>
              <SelectTrigger id="ocelot-version" className="w-full max-w-sm">
                <SelectValue placeholder="Select a version" />
              </SelectTrigger>
              <SelectContent>
                {available.map((candidate) => (
                  <SelectItem key={candidate} value={candidate}>
                    {candidate}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {versionMissing ? (
              <p className="text-xs text-muted-foreground">A version must be selected.</p>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Who is setting this up</CardTitle>
            <CardDescription>
              Recorded against this choice in the audit trail. There is no sign-in yet, so it
              cannot be worked out from a session.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <Label htmlFor="setup-operator">Name</Label>
            <Input
              id="setup-operator"
              value={operator}
              placeholder="e.g. Alex Morgan"
              onChange={(event) => setOperator(event.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              Optional, but the version cannot be changed afterwards, and an unattributed
              permanent decision is hard to audit later.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Operational settings</CardTitle>
            <CardDescription>
              These can be changed later, unlike the version.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-3">
            <NumericField
              id="poll-interval"
              label="Poll interval (seconds)"
              value={draft.pollIntervalSeconds}
              error={errors.pollIntervalSeconds}
              hint="How often a gateway is asked for its configuration."
              onChange={(pollIntervalSeconds) => setDraft({ ...draft, pollIntervalSeconds })}
            />
            <NumericField
              id="audit-retention"
              label="Audit retention (days)"
              value={draft.auditLogRetentionDays}
              error={errors.auditLogRetentionDays}
              hint="0 keeps entries forever."
              onChange={(auditLogRetentionDays) => setDraft({ ...draft, auditLogRetentionDays })}
            />
            <NumericField
              id="snapshot-retention"
              label="Snapshots to keep"
              value={draft.snapshotRetentionCount}
              error={errors.snapshotRetentionCount}
              hint="0 keeps them all."
              onChange={(snapshotRetentionCount) => setDraft({ ...draft, snapshotRetentionCount })}
            />
          </CardContent>
        </Card>

        {error ? <SubmitError error={error} /> : null}

        <Button type="submit" disabled={isSubmitting || versionMissing || Object.keys(errors).length > 0}>
          {isSubmitting ? 'Saving…' : 'Complete setup'}
        </Button>
      </form>
    </div>
  )
}

/**
 * The ordinary case: the version is shown as a fact, because it cannot be edited.
 */
function ConfiguredSettings({
  settings,
  onSave,
  isSaving,
  error,
}: {
  settings: SystemSettingsResponse
  onSave: (draft: SettingsDraft) => void
  isSaving: boolean
  error: unknown
}) {
  const [draft, setDraft] = useState<SettingsDraft>(() => draftFromSettings(settings))
  const errors = settingsErrors(draft)
  const dirty = JSON.stringify(draft) !== JSON.stringify(draftFromSettings(settings))

  useEffect(() => {
    setDraft(draftFromSettings(settings))
  }, [settings])

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold">Settings</h1>
        <p className="text-sm text-muted-foreground">
          System-wide configuration for this control plane.
        </p>
      </header>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Lock aria-hidden="true" className="size-4 text-muted-foreground" />
            Ocelot version
          </CardTitle>
          <CardDescription>
            The version every generated configuration targets. Chosen once at setup and
            permanent, so it is shown rather than offered for editing.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <div className="flex items-center gap-2">
            <span className="font-medium">Ocelot</span>
            <Badge variant="secondary">{settings.ocelotVersion}</Badge>
          </div>
          <dl className="grid gap-1 sm:grid-cols-[180px_1fr] text-muted-foreground">
            <dt>Chosen</dt>
            <dd>{formatTimestamp(settings.ocelotVersionSelectedAt)}</dd>
            <dt>Chosen by</dt>
            <dd>{settings.ocelotVersionSelectedBy ?? '—'}</dd>
          </dl>
        </CardContent>
      </Card>

      <form
        onSubmit={(event) => {
          event.preventDefault()
          if (Object.keys(errors).length > 0) return
          onSave(draft)
        }}
        className="space-y-4"
      >
        <Card>
          <CardHeader>
            <CardTitle>Operational settings</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-3">
            <NumericField
              id="poll-interval"
              label="Poll interval (seconds)"
              value={draft.pollIntervalSeconds}
              error={errors.pollIntervalSeconds}
              hint="How often a gateway is asked for its configuration."
              onChange={(pollIntervalSeconds) => setDraft({ ...draft, pollIntervalSeconds })}
            />
            <NumericField
              id="audit-retention"
              label="Audit retention (days)"
              value={draft.auditLogRetentionDays}
              error={errors.auditLogRetentionDays}
              hint="0 keeps entries forever."
              onChange={(auditLogRetentionDays) => setDraft({ ...draft, auditLogRetentionDays })}
            />
            <NumericField
              id="snapshot-retention"
              label="Snapshots to keep"
              value={draft.snapshotRetentionCount}
              error={errors.snapshotRetentionCount}
              hint="0 keeps them all."
              onChange={(snapshotRetentionCount) => setDraft({ ...draft, snapshotRetentionCount })}
            />
          </CardContent>
        </Card>

        {error ? <SubmitError error={error} /> : null}

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={isSaving || !dirty || Object.keys(errors).length > 0}>
            {isSaving ? 'Saving…' : 'Save'}
          </Button>
          {dirty ? <span className="text-xs text-muted-foreground">Unsaved changes</span> : null}
        </div>
      </form>
    </div>
  )
}

function NumericField({
  id,
  label,
  value,
  error,
  hint,
  onChange,
}: {
  id: string
  label: string
  value: string
  error?: string
  hint?: string
  onChange: (value: string) => void
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="number"
        value={value}
        aria-invalid={Boolean(error)}
        onChange={(event) => onChange(event.target.value)}
      />
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

function SubmitError({ error }: { error: unknown }) {
  const message = error instanceof Error ? error.message : String(error)

  return (
    <Alert variant="destructive">
      <CircleAlert aria-hidden="true" className="size-4" />
      <AlertTitle>{isPermissionError(error) ? 'Not permitted' : 'Could not save'}</AlertTitle>
      <AlertDescription>
        {isPermissionError(error)
          ? 'Changing settings needs the Admin role. Ask an administrator to make this change.'
          : message}
      </AlertDescription>
    </Alert>
  )
}

function SettingsError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  return (
    <Alert variant="destructive">
      <CircleAlert aria-hidden="true" className="size-4" />
      <AlertTitle>Could not load settings</AlertTitle>
      <AlertDescription>
        {error instanceof Error ? error.message : String(error)}
        <Button variant="outline" size="sm" className="ml-3" onClick={onRetry}>
          Try again
        </Button>
      </AlertDescription>
    </Alert>
  )
}

function SettingsSkeleton() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-8 w-48" />
      <Skeleton className="h-32 w-full" />
      <Skeleton className="h-40 w-full" />
    </div>
  )
}

function formatTimestamp(value: string | null): string {
  if (!value) return '—'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}
