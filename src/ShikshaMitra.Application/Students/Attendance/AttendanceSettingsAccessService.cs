using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public class AttendanceSettingsAccessService(IUserScopeAccessService users, StudentScopeAccessService scope,
	IRepository<AcademicYear, Guid> years, IRepository<EducationBoard, Guid> boards, IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationMediumStage, Guid> stages, IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions)
	: ApplicationService, ITransientDependency
{
	public async Task EnsureAsync(Guid? yearId, bool writing)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new BusinessException(AttendancePolicyErrorCodes.TenantRequired);
		var actor = await users.GetCurrentUserScopeAsync();
		if (yearId.HasValue)
		{
			if (!await years.AnyAsync(x => x.Id == yearId && x.TenantId == CurrentTenant.Id)) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
			await scope.EnsureCanAccessAcademicYearAsync(yearId.Value, "AttendanceSettings");
		}
		if ((!yearId.HasValue && !actor.IsAdmin && !actor.HasWildcardAcademicYear) ||
			(writing && yearId.HasValue && !await scope.CurrentUserHasAcademicYearWideScopeAsync(yearId.Value)))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}
	public async Task ValidateAsync(AttendancePolicy policy, Guid? yearId)
	{
		AttendancePolicy.Validate(policy);
		// Load each hierarchy level once; every targeted exception must belong to this tenant and its year.
		var boardIds = policy.Exceptions.Where(x => x.BoardId.HasValue).Select(x => x.BoardId!.Value).ToArray();
		var mediumIds = policy.Exceptions.Where(x => x.MediumId.HasValue).Select(x => x.MediumId!.Value).ToArray();
		var stageIds = policy.Exceptions.Where(x => x.StageId.HasValue).Select(x => x.StageId!.Value).ToArray();
		var standardIds = policy.Exceptions.Where(x => x.StandardId.HasValue).Select(x => x.StandardId!.Value).ToArray();
		var divisionIds = policy.Exceptions.Where(x => x.DivisionId.HasValue).Select(x => x.DivisionId!.Value).ToArray();
		var divisionRows = await divisions.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(divisionIds, x.Id));
		standardIds = standardIds.Concat(divisionRows.Select(x => x.StandardId)).Distinct().ToArray();
		var standardRows = await standards.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, x.Id));
		stageIds = stageIds.Concat(standardRows.Select(x => x.EducationMediumStageId)).Distinct().ToArray();
		var stageRows = await stages.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(stageIds, x.Id));
		mediumIds = mediumIds.Concat(stageRows.Select(x => x.EducationMediumId)).Distinct().ToArray();
		var mediumRows = await mediums.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(mediumIds, x.Id));
		boardIds = boardIds.Concat(mediumRows.Select(x => x.EducationBoardId)).Distinct().ToArray();
		var boardRows = await boards.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(boardIds, x.Id));
		foreach (var rule in policy.Exceptions)
		{
			var division = rule.DivisionId.HasValue ? divisionRows.SingleOrDefault(x => x.Id == rule.DivisionId) : null;
			var standard = standardRows.SingleOrDefault(x => x.Id == (rule.StandardId ?? division?.StandardId));
			var stage = stageRows.SingleOrDefault(x => x.Id == (rule.StageId ?? standard?.EducationMediumStageId));
			var medium = mediumRows.SingleOrDefault(x => x.Id == (rule.MediumId ?? stage?.EducationMediumId));
			var board = boardRows.SingleOrDefault(x => x.Id == (rule.BoardId ?? medium?.EducationBoardId));
			if ((rule.DivisionId.HasValue && division == null) || (rule.StandardId.HasValue && standard == null) ||
				(rule.StageId.HasValue && stage == null) || (rule.MediumId.HasValue && medium == null) || board == null ||
				(yearId.HasValue && board.AcademicYearId != yearId) || (division != null && division.StandardId != standard?.Id) ||
				(standard != null && standard.EducationMediumStageId != stage?.Id) || (stage != null && stage.EducationMediumId != medium?.Id) ||
				(medium != null && medium.EducationBoardId != board.Id)) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		}
	}
	public async Task EnsureEffectiveDateAsync(Guid? yearId, DateTime date)
	{
		if (!yearId.HasValue) return;
		var year = await years.GetAsync(yearId.Value);
		if (date.Date < year.StartDate.Date || date.Date > year.EndDate.Date) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
	}
}
