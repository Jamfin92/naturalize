import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'

import { AppShell } from '@/components/layout/app-shell'
import { ThemeProvider } from '@/components/theme-provider'
import { AuthProvider, RequireAuth } from '@/lib/auth'

import { LoginPage } from '@/pages/login'
import { ApplicantsPage } from '@/pages/applicants'
import { ApplicantDetailPage } from '@/pages/applicant-detail'
import { ApplicantFormPage } from '@/pages/applicant-form'
import { ReportsPage } from '@/pages/reports'

/*
 * This build is scoped to applicants + reports. The dashboard, case queue and
 * approvals screens live on the `enhancement/case-workflow` branch.
 */
export default function App() {
  return (
    <ThemeProvider>
      {/*
       * AuthProvider sits INSIDE BrowserRouter: it redirects to the sign-in page
       * when a token expires, so it needs router hooks. Outside, useNavigate throws.
       */}
      <BrowserRouter>
        <AuthProvider>
          <TooltipProvider delayDuration={200}>
            <Routes>
              <Route path="/" element={<LoginPage />} />

              <Route
                element={
                  <RequireAuth>
                    <AppShell />
                  </RequireAuth>
                }
              >
                <Route path="/applicants" element={<ApplicantsPage />} />
                <Route path="/applicants/new" element={<ApplicantFormPage />} />
                <Route path="/applicants/:id" element={<ApplicantDetailPage />} />
                <Route path="/applicants/:id/edit" element={<ApplicantFormPage />} />
                <Route path="/reports" element={<ReportsPage />} />
              </Route>

              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
            <Toaster />
          </TooltipProvider>
        </AuthProvider>
      </BrowserRouter>
    </ThemeProvider>
  )
}
