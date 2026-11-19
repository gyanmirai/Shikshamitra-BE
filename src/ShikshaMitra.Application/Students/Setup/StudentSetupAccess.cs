using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Setup;

[RemoteService(false)]
public class StudentSetupAccess(
	IIdentityUserRepository users, IRepository<AcademicYear, Guid> years,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards,
	IRepository<EducationMediumStage, Guid> stages, IRepository<Standard, Guid> standards,
	IUserScopeAccessService scopes, IPermissionChecker permissions) : ApplicationService
{
	private static readonly IReadOnlyDictionary<string, (string View, string Manage)> Policies = new Dictionary<string, (string, string)> {
		["admission-periods"] = (ShikshaMitraSubmodulePermissions.Students.Admissions.View, ShikshaMitraSubmodulePermissions.Students.Admissions.Add),
		["admission-forms"] = (ShikshaMitraSubmodulePermissions.Students.AdmissionForms.View, ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Add),
		["admission-stages"] = (ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View, ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Add),
		["admission-offerings"] = (ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View, ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add),
		["public-inquiry-links"] = (ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View, ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add),
		["gr-number"] = (ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View, ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Add),
		["roll-number"] = (ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View, ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Add),
		["houses"] = (ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.View, ShikshaMitraSubmodulePermissions.Students.Records_HouseManagement.Add),
		["records-policy"] = (ShikshaMitraSubmodulePermissions.Students.Records_Settings.View, ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add)
	};
	public sealed record Context(AcademicYear Year, Guid? MediumId, Guid? StandardId,
		Guid? BoardId = null, Guid? StageId = null, bool CanManageYear = false);

	public async Task<Guid> RequireIdentityAsync()
	{
		if (!CurrentUser.IsAuthenticated || CurrentTenant.Id is not Guid tenantId || CurrentUser.TenantId != tenantId || CurrentUser.Id is not Guid actorId)
			throw new AbpAuthorizationException();
		var actor = await users.FindAsync(actorId);
		if (actor?.TenantId != tenantId || !actor.IsActive) throw new AbpAuthorizationException();
		return tenantId;
	}

	public async Task<List<StudentSetupSettingStateDto>> ReadPermissionsAsync()
	{
		var names = Policies.Values.SelectMany(policy => new[] { policy.View, policy.Manage }).Distinct(StringComparer.Ordinal).ToArray();
		var grants = (await permissions.IsGrantedAsync(names)).Result;
		var result = new List<StudentSetupSettingStateDto>();
		foreach (var (key, policy) in Policies)
		{
			var view = grants[policy.View] == PermissionGrantResult.Granted;
			result.Add(new() { Key = key, CanView = view, CanManage = view && grants[policy.Manage] == PermissionGrantResult.Granted });
		}
		return result;
	}

	public async Task<bool> CanAccessSetupAsync()
	{
		// Visit status only needs any View grant, not the setup editor's Manage capabilities.
		var names = Policies.Values.Select(policy => policy.View).Distinct(StringComparer.Ordinal).ToArray();
		var grants = (await permissions.IsGrantedAsync(names)).Result;
		return names.Any(name => grants[name] == PermissionGrantResult.Granted);
	}

	public async Task RequireVisitPermissionAsync(string? key)
	{
		if (key is null or "welcome")
		{
			if (await CanAccessSetupAsync()) return;
		}
		else
		{
			if (!Policies.TryGetValue(key, out var policy)) throw new BusinessException(StudentSetupKeys.Invalid);
			if (await permissions.IsGrantedAsync(policy.View)) return;
		}
		throw new AbpAuthorizationException();
	}

	public async Task<Context> LoadContextAsync(Guid yearId, StudentSetupContextInput input)
	{
		var tenantId = await RequireIdentityAsync();
		var year = await years.FindAsync(x => x.Id == yearId && x.TenantId == tenantId) ?? throw new EntityNotFoundException(typeof(AcademicYear), yearId);
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (scope.TenantId != tenantId || !(scope.HasAcademicYearWideScope(yearId) || scope.AcademicYearIds.Contains(yearId))) throw new AbpAuthorizationException();
		if (input.StandardId.HasValue && !input.AcademicMediumId.HasValue) throw new BusinessException(StudentSetupKeys.Invalid);
		var yearWide = scope.HasAcademicYearWideScope(yearId);
		if (input.AcademicMediumId is not Guid mediumId) return new(year, null, null, CanManageYear: yearWide);
		var medium = await mediums.FindAsync(x => x.Id == mediumId && x.TenantId == tenantId) ?? throw new EntityNotFoundException(typeof(EducationMedium), mediumId);
		var board = await boards.FindAsync(x => x.Id == medium.EducationBoardId && x.TenantId == tenantId && x.AcademicYearId == yearId)
			?? throw new EntityNotFoundException(typeof(EducationMedium), mediumId);
		var broadMedium = scope.HasAcademicYearWideScope(yearId) || scope.WildcardMediumBoardIds.Contains(board.Id) || scope.WildcardMediumStageMediumIds.Contains(mediumId);
		if (!broadMedium && !scope.MediumIds.Contains(mediumId)) throw new AbpAuthorizationException();
		if (input.StandardId is not Guid standardId) return new(year, mediumId, null, board.Id, CanManageYear: yearWide);
		var standard = await standards.FindAsync(x => x.Id == standardId && x.TenantId == tenantId) ?? throw new EntityNotFoundException(typeof(Standard), standardId);
		var stage = await stages.FindAsync(x => x.Id == standard.EducationMediumStageId && x.TenantId == tenantId && x.EducationMediumId == mediumId)
			?? throw new EntityNotFoundException(typeof(Standard), standardId);
		if (!broadMedium && !scope.WildcardStandardMediumStageIds.Contains(stage.Id) && !scope.StandardIds.Contains(standardId) && !scope.WildcardDivisionStandardIds.Contains(standardId))
			throw new AbpAuthorizationException();
		return new(year, mediumId, standardId, board.Id, stage.Id, yearWide);
	}
}
