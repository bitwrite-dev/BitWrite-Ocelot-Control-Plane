import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

import './index.css'
import { AppRouter } from './router.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {/* Roles are not resolved yet — see #433. Until the auth strategy lands the
        shell renders the full navigation and the API enforces access. */}
    <AppRouter />
  </StrictMode>,
)
