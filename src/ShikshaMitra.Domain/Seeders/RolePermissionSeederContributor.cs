using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class RolePermissionSeederContributor : BaseSeederContributor
{
	private const string RoleProviderName = "R";

	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IPermissionGrantSeedRepository _permissionGrantSeedRepository;

	public RolePermissionSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IIdentityRoleRepository roleRepository,
		IPermissionGrantSeedRepository permissionGrantSeedRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_roleRepository = roleRepository;
		_permissionGrantSeedRepository = permissionGrantSeedRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var rolePermissions = BuildRolePermissions();
		var rolesByNormalizedName = (await _roleRepository.GetListAsync())
			.Where(role => !string.IsNullOrWhiteSpace(role.Name))
			.ToDictionary(
				role => role.NormalizedName,
				role => role,
				StringComparer.OrdinalIgnoreCase);

		foreach (var (roleName, permissions) in rolePermissions)
		{
			if (!rolesByNormalizedName.TryGetValue(roleName.ToUpperInvariant(), out var role))
			{
				throw new Exception($"Required seeded role '{roleName}' was not found while assigning baseline permissions.");
			}

			var effectivePermissions = GetEffectivePermissions(roleName, permissions);

			await _permissionGrantSeedRepository.InsertRoleGrantsIgnoringDuplicatesAsync(
				role.Name,
				effectivePermissions,
				CurrentTenant.Id);
		}
	}

	private string[] GetEffectivePermissions(string roleName, string[] permissions)
	{
		if (CurrentTenant.Id != null || !IsHostAdministratorRole(roleName))
		{
			return permissions
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		return permissions
			.Concat(HostPlatformPermissions())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static bool IsHostAdministratorRole(string roleName) =>
		roleName.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
		roleName.Equals("super-admin", StringComparison.OrdinalIgnoreCase);

	internal static Dictionary<string, string[]> BuildRolePermissions()
	{
		var tenantAdminPermissions = TenantAdminPermissions();
		var applicationPermissions = AllApplicationPermissions();
		var leaveSelfServicePermissions = LeaveSelfServicePermissions();
		var academicStaffPermissions = ModulePermissions(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.Students,
			ShikshaMitraModulePermissions.Academics,
			ShikshaMitraModulePermissions.ClassManagement,
			ShikshaMitraModulePermissions.Calendar)
			.Concat(leaveSelfServicePermissions)
			.ToArray();
		var officeStaffPermissions = ModulePermissions(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.Students,
			ShikshaMitraModulePermissions.HR,
			ShikshaMitraModulePermissions.Communication,
			ShikshaMitraModulePermissions.Calendar,
			ShikshaMitraModulePermissions.InstituteSetup);
		var financeStaffPermissions = ModulePermissions(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.Students,
			ShikshaMitraModulePermissions.Finance)
			.Concat(leaveSelfServicePermissions)
			.ToArray();
		var hrStaffPermissions = ModulePermissions(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.HR,
			ShikshaMitraModulePermissions.Communication,
			ShikshaMitraModulePermissions.Calendar,
			ShikshaMitraModulePermissions.InstituteSetup);
		var studentFamilyPermissions = ModulePermissions(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.Students,
			ShikshaMitraModulePermissions.Calendar);
		var admissionsReceptionistPermissions = AdmissionCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignOwner,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ArchiveReopen,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks);
		var admissionsCounselorPermissions = AdmissionCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignOwner,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SetPriority,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeStage,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTasks,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ArchiveReopen,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.VerifyRegistration,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmOriginalSeen,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness);
		var admissionsApproverPermissions = AdmissionCrmPermissions(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Analytics,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatPlans,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageSeatReservations,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageOffers,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveOffers,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageWaitlist,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.MakeAdmissionDecision,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideSeatCapacity,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishExamResults,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamResult,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideEligibility,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideAdmissionReadiness,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PreviewActivation,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveActivation,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RepairActivation);

		// Seed baseline access for deterministic staff personas. Tenant admins can still adjust
		// grants through the Security module; this only inserts missing grants.
		return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			["admin"] = tenantAdminPermissions,
			["super-admin"] = tenantAdminPermissions,
			["Director"] = tenantAdminPermissions,
			["Principal"] = applicationPermissions,
			["Vice Principal"] = applicationPermissions,
			["Administrative Manager"] = applicationPermissions,
			["IT Administrator"] = applicationPermissions,
			["HOD"] = academicStaffPermissions,
			["Teacher"] = academicStaffPermissions,
			["Clerk"] = officeStaffPermissions,
			["Office Assistant"] = officeStaffPermissions,
			["Finance Officer"] = financeStaffPermissions,
			["HR Manager"] = hrStaffPermissions.Concat(HrStaffPlatformPermissions()).ToArray(),
			["Admissions Receptionist"] = admissionsReceptionistPermissions,
			["Admissions Counselor"] = admissionsCounselorPermissions,
			["Admissions Approver"] = admissionsApproverPermissions,
			["Student"] = studentFamilyPermissions,
			["Parent"] = studentFamilyPermissions
		};
	}

	private static string[] AdmissionCrmPermissions(params string[] permissions) =>
	[
		ShikshaMitraModulePermissions.Students,
		ShikshaMitraSubModulePermissions.Enrollment,
		ShikshaMitraSubmodulePermissions.Students.Default,
		"Students.Enrollment.AdmissionsCrm",
		.. permissions
	];

	private static string[] LeaveSelfServicePermissions() =>
	[
		ShikshaMitraModulePermissions.HR,
		ShikshaMitraSubModulePermissions.LeaveManagement,
		ShikshaMitraSubmodulePermissions.HR.Default,
		ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View,
		ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.Add,
		ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.Delete
	];

	private static string[] ModulePermissions(params string[] modulePermissions)
	{
		var detailedModuleKeys = modulePermissions
			.Select(ToDetailedModuleKey)
			.Where(key => key != null)
			.Cast<string>()
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		return modulePermissions
			.Concat(ShikshaMitraSubModulePermissions.All
				.Where(permission => BelongsToAnyModule(permission, modulePermissions)))
			.Concat(ShikshaMitraSubmodulePermissions.AllModules
				.Where(module => detailedModuleKeys.Contains(module.Key))
				.Select(module => module.Key))
			.Concat(ShikshaMitraSubmodulePermissions.AllModules
				.Where(module => detailedModuleKeys.Contains(module.Key))
				.SelectMany(module => module.Value.Submodules.Values)
				.SelectMany(submodule => submodule.Permissions))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static string? ToDetailedModuleKey(string modulePermission)
	{
		var prefix = $"{ShikshaMitraModulePermissions.GroupName}.";
		return modulePermission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
			? modulePermission[prefix.Length..]
			: null;
	}

	private static bool BelongsToAnyModule(string permission, IEnumerable<string> modulePermissions) =>
		modulePermissions.Any(modulePermission =>
			permission.StartsWith($"{modulePermission}.", StringComparison.OrdinalIgnoreCase));

	private static string[] AllApplicationPermissions() =>
			AllModulePermissions()
			.Concat(AllDetailedModulePermissions())
			.Concat(AllClassicSubModulePermissions())
			.Concat(AllDetailedSubmodulePermissions())
			.Concat(AllPlatformPermissions())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

	private static string[] TenantAdminPermissions() =>
		AllApplicationPermissions()
			.Concat(TenantAdminPermissionRepairs())
			.Concat(TenantAdminPlatformPermissions())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

	private static IEnumerable<string> AllModulePermissions() =>
		ShikshaMitraModulePermissions.All;

	private static IEnumerable<string> AllDetailedModulePermissions() =>
		ShikshaMitraSubmodulePermissions.AllModules.Keys;

	private static IEnumerable<string> AllClassicSubModulePermissions() =>
		ShikshaMitraSubModulePermissions.All;

	private static IEnumerable<string> AllDetailedSubmodulePermissions() =>
		ShikshaMitraSubmodulePermissions.AllModules
			.SelectMany(module => module.Value.Submodules.Values)
			.SelectMany(submodule => submodule.Permissions);

	private static IEnumerable<string> TenantAdminPermissionRepairs()
	{
		yield return ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View;
		yield return ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.Add;

		foreach (var permission in ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.All)
		{
			yield return permission;
		}
	}

	private static IEnumerable<string> AllPlatformPermissions()
	{
		yield return "ShikshaMitra.Group";
		yield return "ShikshaMitra.Group.List";
		yield return "ShikshaMitra.Group.View";
		yield return "ShikshaMitra.Group.Add";
		yield return "ShikshaMitra.Group.Create";
		yield return "ShikshaMitra.Group.Edit";
		yield return "ShikshaMitra.Group.Delete";
		yield return TenantManagementPermissions.Tenants.Default;
	}

	private static IEnumerable<string> TenantAdminPlatformPermissions()
	{
		yield return "ShikshaMitra.UserScope";
		yield return "ShikshaMitra.UserScope.Assign";
		yield return "ShikshaMitra.UserScope.Revoke";
		yield return "ShikshaMitra.UserScope.View";
		yield return "ShikshaMitra.UserScope.BulkAssign";
		yield return "ShikshaMitra.UserScope.Manage";
		yield return "AbpPermissionManagement.Permissions";
		yield return "AbpPermissionManagement.Permissions.Manage";
		yield return TenantManagementPermissions.Tenants.Update;
		yield return IdentityPermissions.Users.Default;
		yield return "AbpIdentity.Users.Add";
		yield return "AbpIdentity.Users.Create";
		yield return "AbpIdentity.Users.View";
		yield return IdentityPermissions.Users.ManagePermissions;
		yield return IdentityPermissions.Roles.Default;
		yield return "AbpIdentity.Roles.Add";
		yield return "AbpIdentity.Roles.Create";
		yield return "AbpIdentity.Roles.View";
		yield return IdentityPermissions.Roles.ManagePermissions;

		// New Attendance & Leave Submodule Permissions
		yield return "HR.AttendanceAndLeave.Dashboard.Add";
		yield return "HR.AttendanceAndLeave.Dashboard.Delete";
		yield return "HR.AttendanceAndLeave.AttendanceTracking.Delete";
		yield return "HR.AttendanceAndLeave.Setting.ShiftManagement.View";
		yield return "HR.AttendanceAndLeave.Setting.ShiftManagement.Add";
		yield return "HR.AttendanceAndLeave.Setting.ShiftManagement.Delete";

		// New Performance Management Submodule Permissions
		yield return "HR.PerformanceManagement.Overview.View";
		yield return "HR.PerformanceManagement.Overview.Add";
		yield return "HR.PerformanceManagement.Overview.Delete";
		yield return "HR.PerformanceManagement.Outcomes.View";
		yield return "HR.PerformanceManagement.Outcomes.Add";
		yield return "HR.PerformanceManagement.Outcomes.Delete";
		yield return "HR.PerformanceManagement.Reports.View";
		yield return "HR.PerformanceManagement.Reports.Add";
		yield return "HR.PerformanceManagement.Reports.Delete";
		yield return "HR.PerformanceManagement.Settings.Setup.View";
		yield return "HR.PerformanceManagement.Settings.Setup.Add";
		yield return "HR.PerformanceManagement.Settings.Setup.Delete";
		yield return "HR.PerformanceManagement.ReviewEmployees.View";
		yield return "HR.PerformanceManagement.ReviewEmployees.Add";
		yield return "HR.PerformanceManagement.ReviewEmployees.Delete";
		yield return "HR.PerformanceManagement.ManageCycles.View";
		yield return "HR.PerformanceManagement.ManageCycles.Add";
		yield return "HR.PerformanceManagement.ManageCycles.Delete";

		// Module Visibility Permission
		yield return "ShikshaMitra.Module.HR.PerformanceManagement.Settings";
	}

	private static IEnumerable<string> HrStaffPlatformPermissions()
	{
		yield return IdentityPermissions.Roles.Default;
		yield return "AbpIdentity.Roles.Add";
		yield return "AbpIdentity.Roles.Create";
		yield return "AbpIdentity.Roles.View";
	}

	private static IEnumerable<string> HostPlatformPermissions()
	{
		yield return TenantManagementPermissions.Tenants.Create;
		yield return "AbpTenantManagement.Tenants.Add";
		yield return TenantManagementPermissions.Tenants.Delete;
		yield return TenantManagementPermissions.Tenants.ManageConnectionStrings;
	}

}
