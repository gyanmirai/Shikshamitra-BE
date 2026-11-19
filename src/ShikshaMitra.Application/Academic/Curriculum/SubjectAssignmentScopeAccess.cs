using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(false)]
public class SubjectAssignmentScopeAccess(IRepository<Standard, Guid> standards, AcademicHierarchyQueryScope hierarchy) : ApplicationService
{
	private async Task<IQueryable<Standard>> TenantStandardsAsync()
	{
		var tenant = CurrentTenant.Id;
		return (await standards.GetQueryableAsync()).Where(x => tenant.HasValue && x.TenantId == tenant &&
			x.EducationMediumStage.TenantId == tenant && x.EducationMediumStage.EducationMedium.TenantId == tenant &&
			x.EducationMediumStage.EducationMedium.EducationBoard.TenantId == tenant &&
			x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYear.TenantId == tenant);
	}

	public async Task ValidateContextAsync(AssignedSubject assignment, Guid? mediumId = null)
	{
		if (assignment.TenantId != CurrentTenant.Id || !await AsyncExecuter.AnyAsync((await TenantStandardsAsync()).Where(x =>
			x.Id == assignment.StandardId && (!mediumId.HasValue || mediumId == Guid.Empty || x.EducationMediumStage.EducationMediumId == mediumId))))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	public async Task<IQueryable<AssignedSubject>> FilterAsync(IQueryable<AssignedSubject> query)
	{
		var allowed = (await hierarchy.StandardsAsync(await TenantStandardsAsync())).Select(x => x.Id);
		return query.Where(x => x.TenantId == CurrentTenant.Id && allowed.Contains(x.StandardId));
	}

	public async Task<IQueryable<AssignedSubject>> TenantAssignmentsAsync(IQueryable<AssignedSubject> query)
	{
		var valid = (await TenantStandardsAsync()).Select(x => x.Id);
		return query.Where(x => x.TenantId == CurrentTenant.Id && valid.Contains(x.StandardId));
	}

	public async Task EnsureAsync(AssignedSubject assignment)
	{
		if (!await AsyncExecuter.AnyAsync((await hierarchy.StandardsAsync(await TenantStandardsAsync())).Where(x => x.Id == assignment.StandardId)))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}
}
