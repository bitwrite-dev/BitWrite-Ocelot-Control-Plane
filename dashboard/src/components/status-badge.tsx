import { Badge } from '@/components/ui/badge'
import { TONE_CLASSES, toneForStatus } from '@/lib/status-tone'

/**
 * Status badge with a tone per status. See `lib/status-tone.ts` for the mapping
 * and why the values are not invented here.
 */
export function StatusBadge({ status, className }: { status: string; className?: string }) {
  return (
    <Badge
      variant="secondary"
      className={`${TONE_CLASSES[toneForStatus(status)]} ${className ?? ''}`}
    >
      {status}
    </Badge>
  )
}

/** Boolean state used for routes, plugins and gateways, which expose `isEnabled`. */
export function EnabledBadge({ isEnabled }: { isEnabled: boolean }) {
  return <StatusBadge status={isEnabled ? 'Enabled' : 'Disabled'} />
}
