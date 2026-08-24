using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SimpleStateChecking;
using Volo.Abp.Users;

namespace ShikshaMitra.Security.Permissions;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPermissionChecker))]
public class ShikshaMitraPermissionChecker : PermissionChecker
{
	private static readonly string[] ClassTeacherOperationalPermissionPrefixes =
	[
		"Academics.CurriculumManagement.",
		"Academics.Timetable.",
		"Academics.HomeworkAndAssignment.",
		"Academics.Examination.",
		"ClassManagement.ChaptersProgress."
	];

	private static readonly string[] ClassTeacherOperationalAcademicRoots =
	[
		"Academics.Curriculum",
		"Academics.CurriculumManagement",
		"Academics.Timetable",
		"Academics.HomeworkAssignment",
		"Academics.HomeworkAndAssignment",
		"Academics.Examination",
		"ClassManagement.ChaptersProgress"
	];

	private readonly ICurrentUser _currentUser;
	private readonly IClassTeacherAccessService _classTeacherAccessService;
	private readonly IReportingManagerAccessService _reportingManagerAccessService;

	public ShikshaMitraPermissionChecker(
		ICurrentPrincipalAccessor currentPrincipalAccessor,
		IPermissionDefinitionManager permissionDefinitionManager,
		ICurrentTenant currentTenant,
		IPermissionValueProviderManager permissionValueProviderManager,
		ISimpleStateCheckerManager<PermissionDefinition> stateCheckerManager,
		ICurrentUser currentUser,
		IClassTeacherAccessService classTeacherAccessService,
		IReportingManagerAccessService reportingManagerAccessService)
		: base(currentPrincipalAccessor, permissionDefinitionManager, currentTenant, permissionValueProviderManager, stateCheckerManager)
	{
		_currentUser = currentUser;
		_classTeacherAccessService = classTeacherAccessService;
		_reportingManagerAccessService = reportingManagerAccessService;
	}

	public override async Task<bool> IsGrantedAsync(string name)
	{
		if (await base.IsGrantedAsync(name))
		{
			return true;
		}

		if (name.EndsWith(".Create"))
		{
			var addPermissionName = name[..^7] + ".Add";
			if (await base.IsGrantedAsync(addPermissionName))
			{
				return true;
			}
		}

		if (name.EndsWith(".Add"))
		{
			var createPermissionName = name[..^4] + ".Create";
			if (await base.IsGrantedAsync(createPermissionName))
			{
				return true;
			}
		}

		if (TryGetAcademicOperationAddPermission(name, out var mappedAddPermissionName) &&
			await base.IsGrantedAsync(mappedAddPermissionName))
		{
			return true;
		}

		if (name.EndsWith(".View"))
		{
			var baseName = name[..^5];
			if (baseName == "AbpIdentity.Users" || baseName == "AbpIdentity.Roles" ||
			    baseName == "HR.PerformanceManagement.ReviewEmployees" ||
			    baseName == "HR.PerformanceManagement.ManageCycles")
			{
				if (await base.IsGrantedAsync(baseName))
				{
					return true;
				}
			}
		}

		if (name == "AbpIdentity.Users" || name == "AbpIdentity.Roles" ||
		    name == "HR.PerformanceManagement.ReviewEmployees" ||
		    name == "HR.PerformanceManagement.ManageCycles")
		{
			if (await base.IsGrantedAsync(name + ".View") ||
			    await base.IsGrantedAsync(name + ".Add") ||
			    await base.IsGrantedAsync(name + ".Delete"))
			{
				return true;
			}
		}

		return _currentUser.Id.HasValue &&
			(await IsDerivedClassTeacherPermissionAsync(_currentUser.Id.Value, name) ||
			 await IsDerivedReportingManagerPermissionAsync(_currentUser.Id.Value, name));
	}

	public override async Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name)
	{
		if (await base.IsGrantedAsync(claimsPrincipal, name))
		{
			return true;
		}

		if (name.EndsWith(".Create"))
		{
			var addPermissionName = name[..^7] + ".Add";
			if (await base.IsGrantedAsync(claimsPrincipal, addPermissionName))
			{
				return true;
			}
		}

		if (name.EndsWith(".Add"))
		{
			var createPermissionName = name[..^4] + ".Create";
			if (await base.IsGrantedAsync(claimsPrincipal, createPermissionName))
			{
				return true;
			}
		}

		if (TryGetAcademicOperationAddPermission(name, out var mappedAddPermissionName) &&
			await base.IsGrantedAsync(claimsPrincipal, mappedAddPermissionName))
		{
			return true;
		}

		if (name.EndsWith(".View"))
		{
			var baseName = name[..^5];
			if (baseName == "AbpIdentity.Users" || baseName == "AbpIdentity.Roles" ||
			    baseName == "HR.PerformanceManagement.ReviewEmployees" ||
			    baseName == "HR.PerformanceManagement.ManageCycles")
			{
				if (await base.IsGrantedAsync(claimsPrincipal, baseName))
				{
					return true;
				}
			}
		}

		if (name == "AbpIdentity.Users" || name == "AbpIdentity.Roles" ||
		    name == "HR.PerformanceManagement.ReviewEmployees" ||
		    name == "HR.PerformanceManagement.ManageCycles")
		{
			if (await base.IsGrantedAsync(claimsPrincipal, name + ".View") ||
			    await base.IsGrantedAsync(claimsPrincipal, name + ".Add") ||
			    await base.IsGrantedAsync(claimsPrincipal, name + ".Delete"))
			{
				return true;
			}
		}

		return TryGetUserId(claimsPrincipal, out var userId) &&
			(await IsDerivedClassTeacherPermissionAsync(userId, name) ||
			 await IsDerivedReportingManagerPermissionAsync(userId, name));
	}

	private async Task<bool> IsDerivedClassTeacherPermissionAsync(Guid userId, string permissionName)
	{
		if (!IsClassTeacherOperationalAcademicPermission(permissionName))
		{
			return false;
		}

		return await _classTeacherAccessService.HasAssignmentAsync(userId);
	}

	private async Task<bool> IsDerivedReportingManagerPermissionAsync(Guid userId, string permissionName)
	{
		return permissionName.EndsWith(".View", StringComparison.Ordinal) &&
			IsClassTeacherOperationalAcademicPermission(permissionName) &&
			await _reportingManagerAccessService.HasReporteesAsync(userId);
	}

	private static bool IsClassTeacherOperationalAcademicPermission(string permissionName)
	{
		if (string.Equals(permissionName, "Academics", StringComparison.Ordinal))
		{
			return true;
		}

		// Academic settings are tenant-wide configuration. A class teacher must receive
		// an explicit role/user grant for them; an assigned class alone is not enough.
		return !permissionName.EndsWith(".Settings", StringComparison.Ordinal) &&
			(ClassTeacherOperationalAcademicRoots.Contains(permissionName, StringComparer.Ordinal) ||
			ClassTeacherOperationalPermissionPrefixes.Any(prefix =>
			permissionName.StartsWith(prefix, StringComparison.Ordinal) &&
			!permissionName.Contains(".Settings.", StringComparison.Ordinal)));
	}

	private static bool TryGetAcademicOperationAddPermission(string permissionName, out string addPermissionName)
	{
		addPermissionName = string.Empty;
		if (!permissionName.StartsWith("Academics.", StringComparison.Ordinal) ||
			permissionName.Contains(".Settings.", StringComparison.Ordinal) ||
			permissionName.EndsWith(".Settings", StringComparison.Ordinal) ||
			permissionName.EndsWith(".View", StringComparison.Ordinal) ||
			permissionName.EndsWith(".Delete", StringComparison.Ordinal))
		{
			return false;
		}

		var separatorIndex = permissionName.LastIndexOf('.');
		if (separatorIndex <= "Academics".Length)
		{
			return false;
		}

		addPermissionName = permissionName[..separatorIndex] + ".Add";
		return !string.Equals(addPermissionName, permissionName, StringComparison.Ordinal);
	}

	private static bool TryGetUserId(ClaimsPrincipal? claimsPrincipal, out Guid userId)
	{
		var value = claimsPrincipal?.FindFirst(AbpClaimTypes.UserId)?.Value ??
			claimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		return Guid.TryParse(value, out userId);
	}
}
