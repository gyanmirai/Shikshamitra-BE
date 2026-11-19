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

namespace ShikshaMitra.Academic.Setup;

[RemoteService(false)]
public class AcademicSetupAccess(
	IIdentityUserRepository users, IRepository<AcademicYear, Guid> years,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards,
	IRepository<EducationMediumStage, Guid> stages, IRepository<Standard, Guid> standards,
	IUserScopeAccessService scopes, IPermissionChecker permissions) : ApplicationService
{
	private static readonly IReadOnlyDictionary<string, (string View, string Manage)> Policies = new Dictionary<string, (string, string)> {
		["subjects"] = (ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsSubjectCreation.View, ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsSubjectCreation.Add),
		["elective-groups"] = (ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation.View, ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SettingsElectiveGroupCreation.Add),
		["weekly-lectures"] = (ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.View, ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.Add),
		["lecture-slots"] = (ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.View, ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add),
		["homework-types"] = (ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsHomeworkTypes.View, ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsHomeworkTypes.Add),
		["assignment-types"] = (ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.View, ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Add),
		["rubrics"] = (ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.View, ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.Add),
		["grading-scales"] = (ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsGradeScaling.View, ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsGradeScaling.Add),
		["exam-types"] = (ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsExamTypes.View, ShikshaMitraSubmodulePermissions.Academics.Examination_SettingsExamTypes.Add)
	};
	public sealed record Context(AcademicYear Year, Guid? MediumId, Guid? StandardId);

	public async Task<Guid> RequireIdentityAsync()
	{
		if (!CurrentUser.IsAuthenticated || CurrentTenant.Id is not Guid tenantId || CurrentUser.TenantId != tenantId || CurrentUser.Id is not Guid actorId)
			throw new AbpAuthorizationException();
		var actor = await users.FindAsync(actorId);
		if (actor?.TenantId != tenantId || !actor.IsActive) throw new AbpAuthorizationException();
		return tenantId;
	}

	public async Task<List<AcademicSetupSettingStateDto>> ReadPermissionsAsync()
	{
		var result = new List<AcademicSetupSettingStateDto>();
		foreach (var (key, policy) in Policies)
		{
			var view = await permissions.IsGrantedAsync(policy.View);
			result.Add(new() { Key = key, CanView = view, CanManage = view && await permissions.IsGrantedAsync(policy.Manage) });
		}
		return result;
	}

	public async Task RequireVisitPermissionAsync(string? key)
	{
		if (key is null or "welcome")
		{
			if ((await ReadPermissionsAsync()).Any(x => x.CanView)) return;
		}
		else
		{
			if (!Policies.TryGetValue(key, out var policy)) throw new BusinessException(AcademicSetupKeys.Invalid);
			if (await permissions.IsGrantedAsync(policy.View)) return;
		}
		throw new AbpAuthorizationException();
	}

	public async Task<Context> LoadContextAsync(Guid yearId, AcademicSetupContextInput input)
	{
		var tenantId = await RequireIdentityAsync();
		var year = await years.FindAsync(x => x.Id == yearId && x.TenantId == tenantId) ?? throw new EntityNotFoundException(typeof(AcademicYear), yearId);
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (scope.TenantId != tenantId || !(scope.HasAcademicYearWideScope(yearId) || scope.AcademicYearIds.Contains(yearId))) throw new AbpAuthorizationException();
		if (input.StandardId.HasValue && !input.AcademicMediumId.HasValue) throw new BusinessException(AcademicSetupKeys.Invalid);
		if (input.AcademicMediumId is not Guid mediumId) return new(year, null, null);
		var medium = await mediums.FindAsync(x => x.Id == mediumId && x.TenantId == tenantId) ?? throw new EntityNotFoundException(typeof(EducationMedium), mediumId);
		var board = await boards.FindAsync(x => x.Id == medium.EducationBoardId && x.TenantId == tenantId && x.AcademicYearId == yearId)
			?? throw new EntityNotFoundException(typeof(EducationMedium), mediumId);
		var broadMedium = scope.HasAcademicYearWideScope(yearId) || scope.WildcardMediumBoardIds.Contains(board.Id) || scope.WildcardMediumStageMediumIds.Contains(mediumId);
		if (!broadMedium && !scope.MediumIds.Contains(mediumId)) throw new AbpAuthorizationException();
		if (input.StandardId is not Guid standardId) return new(year, mediumId, null);
		var standard = await standards.FindAsync(x => x.Id == standardId && x.TenantId == tenantId) ?? throw new EntityNotFoundException(typeof(Standard), standardId);
		var stage = await stages.FindAsync(x => x.Id == standard.EducationMediumStageId && x.TenantId == tenantId && x.EducationMediumId == mediumId)
			?? throw new EntityNotFoundException(typeof(Standard), standardId);
		if (!broadMedium && !scope.WildcardStandardMediumStageIds.Contains(stage.Id) && !scope.StandardIds.Contains(standardId) && !scope.WildcardDivisionStandardIds.Contains(standardId))
			throw new AbpAuthorizationException();
		return new(year, mediumId, standardId);
	}
}
