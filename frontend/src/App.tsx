import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './core/context/AuthContext'
import { ToastProvider } from './core/context/ToastContext'
import Layout from './shared/components/Layout'
import ProtectedRoute from './shared/components/ProtectedRoute'
import AdminRoute from './shared/components/AdminRoute'
import RoleRoute from './shared/components/RoleRoute'
import LoginPage from './features/auth/LoginPage'
import RegisterPage from './features/auth/RegisterPage'
import ForgotPasswordPage from './features/auth/ForgotPasswordPage'
import ResetPasswordPage from './features/auth/ResetPasswordPage'
import VerifyEmailPage from './features/auth/VerifyEmailPage'
import ApiaryListPage from './features/apiaries/ApiaryListPage'
import ApiaryDetailPage from './features/apiaries/ApiaryDetailPage'
import ApiaryFormPage from './features/apiaries/ApiaryFormPage'
import BeehiveDetailPage from './features/beehives/BeehiveDetailPage'
import BeehiveFormPage from './features/beehives/BeehiveFormPage'
import InspectionFormPage from './features/inspections/InspectionFormPage'
import FeedingsPage from './features/diets/FeedingsPage'
import DietFormPage from './features/diets/DietFormPage'
import DietDetailPage from './features/diets/DietDetailPage'
import AdminDashboardPage from './features/admin/AdminDashboardPage'
import OrganizationFormPage from './features/admin/OrganizationFormPage'
import UserFormPage from './features/admin/UserFormPage'
import MembersPage from './features/members/MembersPage'
import MemberAssignmentPage from './features/members/MemberAssignmentPage'
import ExpensesPage from './features/expenses/ExpensesPage'
import ExpenseFormPage from './features/expenses/ExpenseFormPage'
import ReceiptScanPage from './features/expenses/ReceiptScanPage'
import HarvestsPage from './features/harvests/HarvestsPage'
import HarvestFormPage from './features/harvests/HarvestFormPage'
import TreatmentsPage from './features/treatments/TreatmentsPage'
import TreatmentFormPage from './features/treatments/TreatmentFormPage'
import TreatmentDetailPage from './features/treatments/TreatmentDetailPage'
import LearningPage from './features/learning/LearningPage'
import LearningTopicPage from './features/learning/LearningTopicPage'
import OutboxPage from './features/offline/OutboxPage'
import PasturesPage from './features/pastures/PasturesPage'
import AnnouncementsPage from './features/announcements/AnnouncementsPage'
import LearningTopicsAdminPage from './features/admin/LearningTopicsAdminPage'
import LearningTopicFormPage from './features/admin/LearningTopicFormPage'
import AnnouncementsAdminPage from './features/admin/AnnouncementsAdminPage'
import AnnouncementFormPage from './features/admin/AnnouncementFormPage'
import FeedbackAdminPage from './features/admin/FeedbackAdminPage'
import AssistantPage from './features/assistant/AssistantPage'
import SmartRedirect from './shared/components/SmartRedirect'
import NotFoundPage from './shared/components/NotFoundPage'
import ScanPage from './features/beehives/ScanPage'
import ProfilePage from './features/profile/ProfilePage'
import StatsPage from './features/stats/StatsPage'
import CalendarPage from './features/calendar/CalendarPage'
import CalendarSettingsPage from './features/calendar/CalendarSettingsPage'
import PlansPage from './features/plans/PlansPage'
import InvitePage from './features/invites/InvitePage'
import UpsellModal from './shared/components/UpsellModal'
import HistoryTracker from './shared/components/HistoryTracker'

const APIARY_MANAGERS   = ['OrganizationAdmin', 'SystemAdmin']
const HIVE_MANAGERS     = ['ApiaryAdmin', 'OrganizationAdmin', 'SystemAdmin']
const MEMBER_MANAGERS   = ['OrganizationAdmin', 'ApiaryAdmin']
const EXPENSE_MANAGERS  = ['ApiaryAdmin', 'OrganizationAdmin', 'SystemAdmin']

export default function App() {
  return (
    <AuthProvider>
      <ToastProvider>
      <BrowserRouter>
        {/* Records each visited entry so a form can tell where Back would land — see historyStack. */}
        <HistoryTracker />
        <UpsellModal />
        <Routes>
          {/* Public routes */}
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          {/* Both are reached from an emailed link, so they must work signed out. */}
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/verify-email" element={<VerifyEmailPage />} />
          <Route path="/scan/:uniqueId" element={<ScanPage />} />

          {/* Protected routes — redirect to /login if not authenticated */}
          <Route element={<ProtectedRoute />}>
            <Route path="/" element={<Layout />}>
              <Route index element={<SmartRedirect />} />

              {/* Apiary list + detail — all authenticated users */}
              <Route path="apiaries"  element={<ApiaryListPage />} />

              {/* Apiary create/edit — OrgAdmin and SystemAdmin only (before :id to avoid conflict) */}
              <Route element={<RoleRoute allowedRoles={APIARY_MANAGERS} />}>
                <Route path="apiaries/new"      element={<ApiaryFormPage />} />
                <Route path="apiaries/:id/edit" element={<ApiaryFormPage />} />
              </Route>

              <Route path="apiaries/:id" element={<ApiaryDetailPage />} />

              {/* Beehive create/edit — Admin, OrgAdmin, SystemAdmin (before :id to avoid conflict) */}
              <Route element={<RoleRoute allowedRoles={HIVE_MANAGERS} />}>
                <Route path="beehives/new"      element={<BeehiveFormPage />} />
                <Route path="beehives/:id/edit" element={<BeehiveFormPage />} />
              </Route>

              {/* Beehive detail — all authenticated users */}
              <Route path="beehives/:id" element={<BeehiveDetailPage />} />

              {/* Inspection create/edit — all authenticated users (User allowed for assigned hives) */}
              <Route path="inspections/new"        element={<InspectionFormPage />} />
              <Route path="inspections/:id/edit"   element={<InspectionFormPage />} />

              {/* Feeding programmes — list + detail for all authenticated users (Beekeeper read-only);
                  create/edit restricted to hive managers (SPEC-12 narrowed this: a programme is
                  chosen across an apiary's hives, which a Beekeeper cannot see) */}
              <Route path="feedings"     element={<FeedingsPage />} />
              <Route path="feedings/:id" element={<DietDetailPage />} />
              <Route element={<RoleRoute allowedRoles={HIVE_MANAGERS} />}>
                <Route path="feedings/new"      element={<DietFormPage />} />
                <Route path="feedings/:id/edit" element={<DietFormPage />} />
              </Route>

              {/* Profile — all authenticated users */}
              <Route path="profile" element={<ProfilePage />} />

              {/* Plans & billing (paketi) — all authenticated users */}
              <Route path="plans" element={<PlansPage />} />

              {/* Invite a friend (SPEC-15) — all authenticated users, including members of
                  someone else's organization: the invitee always gets their own organization. */}
              <Route path="invite" element={<InvitePage />} />

              {/* Stats — all authenticated users */}
              <Route path="stats" element={<StatsPage />} />

              {/* Calendar — all authenticated users */}
              <Route path="calendar" element={<CalendarPage />} />
              <Route path="calendar/settings" element={<CalendarSettingsPage />} />

              {/* AI Assistant (SPEC-17/18) — all authenticated users; the plan gate is server-side (402) */}
              <Route path="assistant" element={<AssistantPage />} />

              {/* Learning (Edukacija) — all authenticated users */}
              <Route path="learning"     element={<LearningPage />} />
              <Route path="learning/:id" element={<LearningTopicPage />} />

              {/* Šta je novo (SPEC-21) — all authenticated users, SystemAdmin included */}
              <Route path="announcements" element={<AnnouncementsPage />} />

              {/* Offline outbox (neposlani pregledi) — all authenticated users */}
              <Route path="outbox" element={<OutboxPage />} />

              {/* Pastures (pašnjaci) — registry readable by all; write actions gated in-page + API */}
              <Route path="pastures" element={<PasturesPage />} />

              {/* Members routes — OrgAdmin and Admin */}
              <Route element={<RoleRoute allowedRoles={MEMBER_MANAGERS} />}>
                <Route path="members"                        element={<MembersPage />} />
                <Route path="members/:id/assignments"        element={<MemberAssignmentPage />} />
              </Route>

              {/* Expenses — Admin, OrgAdmin, SystemAdmin */}
              <Route element={<RoleRoute allowedRoles={EXPENSE_MANAGERS} />}>
                <Route path="expenses"           element={<ExpensesPage />} />
                <Route path="expenses/scan"      element={<ReceiptScanPage />} />
                <Route path="expenses/new"       element={<ExpenseFormPage />} />
                <Route path="expenses/:id/edit"  element={<ExpenseFormPage />} />
              </Route>

              {/* Harvests — list for all authenticated users (Beekeeper read-only);
                  create/edit restricted to hive managers */}
              <Route path="harvests" element={<HarvestsPage />} />
              <Route element={<RoleRoute allowedRoles={HIVE_MANAGERS} />}>
                <Route path="harvests/new"      element={<HarvestFormPage />} />
                <Route path="harvests/:id/edit" element={<HarvestFormPage />} />
              </Route>

              {/* Treatments — list for all authenticated users (Beekeeper read-only);
                  create/edit restricted to hive managers */}
              <Route path="treatments" element={<TreatmentsPage />} />
              <Route path="treatments/:id" element={<TreatmentDetailPage />} />
              <Route element={<RoleRoute allowedRoles={HIVE_MANAGERS} />}>
                <Route path="treatments/new"      element={<TreatmentFormPage />} />
                <Route path="treatments/:id/edit" element={<TreatmentFormPage />} />
              </Route>

              {/* Admin routes — SystemAdmin only */}
              <Route element={<AdminRoute />}>
                <Route path="admin"                             element={<AdminDashboardPage />} />
                <Route path="admin/organizations/new"           element={<OrganizationFormPage />} />
                <Route path="admin/organizations/:id/edit"      element={<OrganizationFormPage />} />
                <Route path="admin/users/new"                   element={<UserFormPage />} />
                <Route path="admin/users/:id/edit"              element={<UserFormPage />} />
                <Route path="admin/learning-topics"             element={<LearningTopicsAdminPage />} />
                <Route path="admin/learning-topics/new"         element={<LearningTopicFormPage />} />
                <Route path="admin/learning-topics/:id/edit"    element={<LearningTopicFormPage />} />
                <Route path="admin/announcements"               element={<AnnouncementsAdminPage />} />
                <Route path="admin/announcements/new"           element={<AnnouncementFormPage />} />
                <Route path="admin/announcements/:id/edit"      element={<AnnouncementFormPage />} />
                <Route path="admin/feedback"                    element={<FeedbackAdminPage />} />
              </Route>
            </Route>
          </Route>

          {/* Unknown path — show a real 404 rather than silently bouncing to the home page. */}
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </BrowserRouter>
      </ToastProvider>
    </AuthProvider>
  )
}
