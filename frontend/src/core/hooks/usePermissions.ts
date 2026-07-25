import { useAuth } from '../context/AuthContext'

export function usePermissions() {
  const { user } = useAuth()
  const role = user?.role

  const isSystemAdmin = role === 'SystemAdmin'
  const isOrgAdmin    = role === 'OrganizationAdmin'
  const isAdmin       = role === 'ApiaryAdmin'
  const isUser        = role === 'Beekeeper'

  return {
    // SystemAdmin only
    canManageOrganizations: isSystemAdmin,
    canManageUsers: isSystemAdmin,

    // OrgAdmin + SystemAdmin
    canManageApiaries: isSystemAdmin || isOrgAdmin,
    canManageApiary: isSystemAdmin || isOrgAdmin,
    // Admin can manage todos on their assigned apiary too
    canManageApiaryTodos: isSystemAdmin || isOrgAdmin || isAdmin,

    // Admin + OrgAdmin + SystemAdmin
    canManageHives: isSystemAdmin || isOrgAdmin || isAdmin,
    canManageDiets: isSystemAdmin || isOrgAdmin || isAdmin,
    canManageInspections: isSystemAdmin || isOrgAdmin || isAdmin,
    canManageHiveTodos: isSystemAdmin || isOrgAdmin || isAdmin,

    // All authenticated users can create inspections (Reviews)
    canCreateInspections: true,

    // Kept for backward compatibility in components that already use this name
    canEditDelete: isSystemAdmin || isOrgAdmin || isAdmin,

    // Navigation visibility — lives here so Layout and Sidebar don't re-derive role rules.
    canSeeExpenses: isSystemAdmin || isOrgAdmin || isAdmin,
    canManageMembers: isOrgAdmin || isAdmin,
    canSeePastures: isOrgAdmin || isSystemAdmin,

    isSystemAdmin,
    isOrgAdmin,
    isAdmin,
    isUser,

    /** Returns true if the current User-role user is assigned to the given hive. */
    isAssignedToHive: (beehiveId: number): boolean =>
      isUser ? (user?.assignedBeehiveIds ?? []).includes(beehiveId) : false,
  }
}
