using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportScopeService(
	LibraryReportRequestGuard requestGuard,
	IUserScopeAccessService userScopeAccessService,
	IRepository<AcademicYear, Guid> academicYearRepository,
	ICurrentTenant currentTenant,
	IUserScopeService userScopes,
	IIdentityUserRepository users,
	IRepository<IdentityRole, Guid> roles,
	IPermissionChecker permissions,
	ICurrentUser currentUser,
	IUnitOfWorkManager units,
	ILibraryAccessService libraryAccess,
	ICurrentPrincipalAccessor principalAccessor)
	: ITransientDependency
{
	public const string ScopeSignatureProperty = "LibraryExportScopeSignature";
	public const string MemberSignatureProperty = "LibraryExportMemberSignature";

	public async Task<LibraryReportExecutionScope> AuthorizeAndNormalizeAsync(LibraryReportRequestDto request)
	{
		requestGuard.ValidateAndNormalize(request);
		var executionScope = await GetCurrentExecutionScopeAsync();
		ValidateAndNormalizeForScope(request, executionScope);
		return executionScope;
	}

	public void ValidateAndNormalizeForScope(LibraryReportRequestDto request, LibraryReportExecutionScope executionScope)
	{
		requestGuard.ValidateAndNormalize(request);
		if (request.Filters.AcademicYearId.HasValue &&
		    !executionScope.AllowedAcademicYearIds.Contains(request.Filters.AcademicYearId.Value))
		{
			throw new BusinessException(LibraryErrorCodes.ReportScopeAccessDenied)
				.WithData("AcademicYearId", request.Filters.AcademicYearId.Value);
		}
	}

	public async Task<LibraryReportExecutionScope> GetCurrentExecutionScopeAsync() =>
		(await ResolveAuthorityAsync(currentUser.Id ?? throw new BusinessException(LibraryErrorCodes.ReportUserRequired))).Scope;

	public async Task<(ClaimsPrincipal Principal, LibraryReportExecutionScope Scope, string? MemberSignature)> ResolveAuthorityAsync(
		Guid userId, bool requireExport = false, bool includeMemberPopulation = false)
	{
		// Authorization has its own unit of work so a long-running export cannot reuse a revoked grant or scope.
		using var read = units.Begin(requiresNew: true, isTransactional: false);
		var identity = await users.FindAsync(userId, includeDetails: true);
		if (identity == null || !identity.IsActive || identity.TenantId != currentTenant.Id) throw new AbpAuthorizationException();
		var roleIds = identity.Roles.Select(role => role.RoleId).ToArray();
		var databaseRoles = await roles.GetListAsync(role => role.TenantId == currentTenant.Id && Enumerable.Contains(roleIds, role.Id));
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, identity.Id.ToString()),
			new(AbpClaimTypes.UserName, identity.UserName)
		};
		if (currentTenant.Id.HasValue) claims.Add(new(AbpClaimTypes.TenantId, currentTenant.Id.Value.ToString()));
		claims.AddRange(databaseRoles.Select(role => new Claim(AbpClaimTypes.Role, role.Name)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "LibraryReport", AbpClaimTypes.UserName, AbpClaimTypes.Role));
		using var actor = principalAccessor.Change(principal);
		if (!await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.Library.Reports.View) ||
			requireExport && !await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.Library.Reports.Export))
			throw new AbpAuthorizationException();
		var userScope = await userScopes.GetUserScopeAsync(userId);
		if (userScope.UserId != userId || userScope.TenantId != currentTenant.Id) throw ScopeChanged();
		userScopeAccessService.EnsureHasAnyAccess(userScope, LibraryErrorCodes.ReportScopeAccessDenied);
		var academicYears = await academicYearRepository.GetListAsync(year => year.TenantId == currentTenant.Id);
		var allowedYearIds = userScope.HasTenantWideScope()
			? academicYears.Select(year => year.Id).ToList()
			: academicYears
				.Where(year => userScope.AcademicYearIds.Contains(year.Id) || userScope.WildcardBoardAcademicYearIds.Contains(year.Id))
				.Select(year => year.Id)
				.ToList();
		if (allowedYearIds.Count == 0)
		{
			throw new BusinessException(LibraryErrorCodes.ReportScopeAccessDenied);
		}

		var executionScope = new LibraryReportExecutionScope
		{
			TenantId = currentTenant.Id,
			UserScope = userScope,
			AllowedAcademicYearIds = allowedYearIds
		};
		var memberSignature = includeMemberPopulation
			? CreateMemberSignature(executionScope.TenantId, await libraryAccess.GetMemberIdsAsync(userScope))
			: null;
		await read.CompleteAsync();
		return (principal, executionScope, memberSignature);
	}

	public void CaptureExportScope(HrReportExportJob job, LibraryReportExecutionScope scope, string? memberSignature)
	{
		if (job.TenantId != currentTenant.Id || scope.TenantId != job.TenantId || scope.UserScope.UserId != job.RequestedByUserId || string.IsNullOrWhiteSpace(memberSignature))
			throw ScopeChanged();
		job.ExtraProperties[ScopeSignatureProperty] = CreateScopeSignature(scope);
		job.ExtraProperties[MemberSignatureProperty] = memberSignature;
	}

	public bool CanAccessStoredScope(HrReportExportJob job, LibraryReportExecutionScope currentScope, string? memberSignature) =>
		job.TenantId == currentTenant.Id && currentScope.TenantId == job.TenantId &&
		currentScope.UserScope.TenantId == job.TenantId &&
		job.ExtraProperties.TryGetValue(ScopeSignatureProperty, out var captured) &&
		string.Equals(captured?.ToString(), CreateScopeSignature(currentScope), StringComparison.Ordinal) &&
		!string.IsNullOrWhiteSpace(memberSignature) && job.ExtraProperties.TryGetValue(MemberSignatureProperty, out var capturedMembers) &&
		string.Equals(capturedMembers?.ToString(), memberSignature, StringComparison.Ordinal);

	public void EnsureStoredScope(HrReportExportJob job, LibraryReportExecutionScope currentScope, string? memberSignature)
	{
		if (!CanAccessStoredScope(job, currentScope, memberSignature)) throw ScopeChanged();
	}

	public static string CreateMemberSignature(Guid? tenantId, IEnumerable<Guid> memberIds) =>
		"v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
		{
			Version = 1, TenantId = tenantId, Members = Sorted(memberIds)
		}))));

	public static string CreateScopeSignature(LibraryReportExecutionScope executionScope)
	{
		var scope = executionScope.UserScope;
		var wide = scope.HasTenantWideScope();
		var canonical = new
		{
			Version = 1, executionScope.TenantId, TenantWide = wide,
			AllowedYears = Sorted(executionScope.AllowedAcademicYearIds),
			Hierarchy = wide ? null : new
			{
				Years = Sorted(scope.AcademicYearIds), Boards = Sorted(scope.BoardIds), Mediums = Sorted(scope.MediumIds),
				Stages = Sorted(scope.MediumStageIds), Standards = Sorted(scope.StandardIds), Divisions = Sorted(scope.DivisionIds),
				scope.HasWildcardBoard, scope.HasWildcardMedium, scope.HasWildcardMediumStage, scope.HasWildcardStandard, scope.HasWildcardDivision,
				AllBoardsInYears = Sorted(scope.WildcardBoardAcademicYearIds), AllMediumsInBoards = Sorted(scope.WildcardMediumBoardIds),
				AllStagesInMediums = Sorted(scope.WildcardMediumStageMediumIds), AllStandardsInStages = Sorted(scope.WildcardStandardMediumStageIds),
				AllDivisionsInStandards = Sorted(scope.WildcardDivisionStandardIds)
			}
		};
		return "v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
	}

	private static Guid[] Sorted(IEnumerable<Guid> values) => values.Distinct().OrderBy(value => value).ToArray();

	private static BusinessException ScopeChanged() => new BusinessException(LibraryErrorCodes.ReportScopeAccessDenied,
		"This export has no matching current authorization snapshot. Generate a new report with your current access.")
		.WithData("RequiresRegeneration", true);
}
