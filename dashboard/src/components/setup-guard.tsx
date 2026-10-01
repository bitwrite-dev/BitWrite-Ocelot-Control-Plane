import { Navigate, useLocation } from 'react-router-dom'

import { useSystemSettings } from '@/features/settings/queries'
import { LoadingState } from '@/components/page-state'

/**
 * Sends an unconfigured installation to setup before anything else.
 *
 * Nothing did this, and the cost was a page that could only report failure. A
 * fresh install answered 401 on setup, refused to build any configuration, and
 * the operator was left to work out from a preview's error message which of
 * several screens to go and fix. Setup is the first thing this product asks for,
 * so it is the first thing it should ask for.
 *
 * The guard holds back every page except setup itself. A failure to read the
 * settings is treated as "configured", not as a reason to trap the operator
 * here: if the API is down, an interstitial that only offers setup would be the
 * wrong screen, and the real page's own error is more honest about what is
 * wrong.
 */
export function SetupGuard({ children }: { children: React.ReactNode }) {
  const location = useLocation()
  const { data, isPending, isError } = useSystemSettings()

  if (isPending) return <LoadingState />

  // The settings could not be read, so nothing is known about this install.
  // Let the page try and fail on its own terms.
  if (isError || !data || data.isInitialised) return <>{children}</>

  if (location.pathname === '/settings') return <>{children}</>

  return (
    <Navigate
      to="/settings"
      replace
      state={{
        from: location.pathname,
        // Said plainly, because the alternative is a page that quietly differs
        // from the one the operator asked for and never says why.
        reason:
          'This installation has not been set up yet. Choose the Ocelot version before anything else — it decides the shape of every configuration this control plane generates.',
      }}
    />
  )
}
