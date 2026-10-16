using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
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
public class ShikshaMitraPermissionChecker : PermissionChecker, IPermissionChecker
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
	private readonly IUserPermissionDenyStore _denies;
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
		IReportingManagerAccessService reportingManagerAccessService,
		IUserPermissionDenyStore denies)
		: base(currentPrincipalAccessor, permissionDefinitionManager, currentTenant, permissionValueProviderManager, stateCheckerManager)
	{
		_currentUser = currentUser;
		_denies = denies;
		_classTeacherAccessService = classTeacherAccessService;
		_reportingManagerAccessService = reportingManagerAccessService;
	}

	public override async Task<bool> IsGrantedAsync(string name)
	{
		if (_currentUser.Id.HasValue && (await _denies.GetDeniedAsync(_currentUser.Id.Value)).Contains(name)) return false;
		// Student actions require their exact role/user grant. Teacher assignments only scope data.
		if (IsStudentPermission(name)) return await base.IsGrantedAsync(name);

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
		if (TryGetUserId(claimsPrincipal, out var deniedUser) && (await _denies.GetDeniedAsync(deniedUser)).Contains(name)) return false;
		if (IsStudentPermission(name)) return await base.IsGrantedAsync(claimsPrincipal, name);

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

	public Task<MultiplePermissionGrantResult> GetEffectiveGrantsAsync(string[] names) =>
		IsGrantedInBatchAsync(names, permissionNames => base.IsGrantedAsync(permissionNames), _currentUser.Id);

	Task<MultiplePermissionGrantResult> IPermissionChecker.IsGrantedAsync(string[] names) =>
		GetEffectiveGrantsAsync(names);

	Task<MultiplePermissionGrantResult> IPermissionChecker.IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names) =>
		IsGrantedInBatchAsync(names, permissionNames => base.IsGrantedAsync(claimsPrincipal, permissionNames),
			TryGetUserId(claimsPrincipal, out var userId) ? userId : null);

	private async Task<MultiplePermissionGrantResult> IsGrantedInBatchAsync(
		string[] names,
		Func<string[], Task<MultiplePermissionGrantResult>> checkBasePermissions,
		Guid? userId)
	{
		var denied = userId.HasValue ? await _denies.GetDeniedAsync(userId.Value) : new HashSet<string>();
		var distinctNames = names.Distinct(StringComparer.Ordinal).ToArray();
		var aliases = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		var aliasNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (var name in distinctNames)
		{
			var candidates = new List<string>();
			if (IsStudentPermission(name))
			{
				aliases[name] = candidates;
				continue;
			}
			if (name.EndsWith(".Create", StringComparison.Ordinal)) candidates.Add(name[..^7] + ".Add");
			if (name.EndsWith(".Add", StringComparison.Ordinal)) candidates.Add(name[..^4] + ".Create");
			if (TryGetAcademicOperationAddPermission(name, out var academicAdd)) candidates.Add(academicAdd);
			if (name.EndsWith(".View", StringComparison.Ordinal))
			{
				var parent = name[..^5];
				if (IsViewAliasRoot(parent)) candidates.Add(parent);
			}
			if (IsViewAliasRoot(name))
			{
				candidates.Add(name + ".View");
				candidates.Add(name + ".Add");
				candidates.Add(name + ".Delete");
			}
			aliases[name] = candidates;
			foreach (var candidate in candidates) aliasNames.Add(candidate);
		}

		var baseGrants = (await checkBasePermissions(
			distinctNames.Concat(aliasNames).Distinct(StringComparer.Ordinal).ToArray())).Result;
		var result = new MultiplePermissionGrantResult();
		var needsClassTeacher = false;
		var needsReportingManager = false;
		foreach (var name in distinctNames)
		{
			if (denied.Contains(name))
			{
				result.Result[name] = PermissionGrantResult.Prohibited;
				continue;
			}
			var granted = baseGrants[name] == PermissionGrantResult.Granted ||
				aliases[name].Any(alias => baseGrants[alias] == PermissionGrantResult.Granted);
			result.Result[name] = granted ? PermissionGrantResult.Granted : PermissionGrantResult.Prohibited;
			if (!granted && userId.HasValue && IsClassTeacherOperationalAcademicPermission(name))
			{
				needsClassTeacher = true;
				needsReportingManager |= name.EndsWith(".View", StringComparison.Ordinal);
			}
		}

		if (!userId.HasValue || !needsClassTeacher) return result;
		var hasAssignment = await _classTeacherAccessService.HasAssignmentAsync(userId.Value);
		if (hasAssignment)
		{
			foreach (var name in distinctNames.Where(name => !denied.Contains(name) && IsClassTeacherOperationalAcademicPermission(name)))
				result.Result[name] = PermissionGrantResult.Granted;
			return result;
		}
		if (needsReportingManager && await _reportingManagerAccessService.HasReporteesAsync(userId.Value))
		{
			foreach (var name in distinctNames.Where(name =>
				!denied.Contains(name) && name.EndsWith(".View", StringComparison.Ordinal) && IsClassTeacherOperationalAcademicPermission(name)))
				result.Result[name] = PermissionGrantResult.Granted;
		}
		return result;
	}

	private static bool IsViewAliasRoot(string name) =>
		name is "AbpIdentity.Users" or "AbpIdentity.Roles" or
			"HR.PerformanceManagement.ReviewEmployees" or "HR.PerformanceManagement.ManageCycles";

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

	private static bool IsStudentPermission(string name) =>
		name == "Students" || name.StartsWith("Students.", StringComparison.Ordinal);
}
