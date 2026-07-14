import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'

import { AppShell } from '@/components/layout/app-shell'
import { ThemeProvider } from '@/components/theme-provider'
import { AuthProvider, RequireAuth } from '@/lib/auth'

import { LoginPage } from '@/pages/login'
import { DashboardPage } from '@/pages/dashboard'
import { ApplicantsPage } from '@/pages/applicants'
import { ApplicantDetailPage } from '@/pages/applicant-detail'
import { CasesPage } from '@/pages/cases'
import { CaseDetailPage } from '@/pages/case-detail'
import { ApprovalsPage } from '@/pages/approvals'
import { ReportsPage } from '@/pages/reports'

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <TooltipProvider delayDuration={200}>
          <BrowserRouter>
            <Routes>
              <Route path="/" element={<LoginPage />} />

              <Route
                element={
                  <RequireAuth>
                    <AppShell />
                  </RequireAuth>
                }
              >
                <Route path="/dashboard" element={<DashboardPage />} />
                <Route path="/applicants" element={<ApplicantsPage />} />
                <Route path="/applicants/:id" element={<ApplicantDetailPage />} />
                <Route path="/cases" element={<CasesPage />} />
                <Route path="/cases/:id" element={<CaseDetailPage />} />
                <Route path="/approvals" element={<ApprovalsPage />} />
                <Route path="/reports" element={<ReportsPage />} />
              </Route>

              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </BrowserRouter>
          <Toaster />
        </TooltipProvider>
      </AuthProvider>
    </ThemeProvider>
  )
}
