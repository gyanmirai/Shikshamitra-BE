using System;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

[RemoteService(false)]
public class SchoolIdentityScopeAccess(
	IRepository<EducationBoard, Guid> boards,
	IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationMediumStage, Guid> organizations,
	IUserScopeAccessService scopes) : ApplicationService, ITransientDependency
{
	public async Task<Guid?> EnsureAsync(Guid? boardId, Guid? mediumId, bool manage = false, Guid? organizationId = null)
	{
		if (!CurrentTenant.Id.HasValue || (mediumId.HasValue && !boardId.HasValue)
			|| (organizationId.HasValue && !mediumId.HasValue))
			throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		if (!boardId.HasValue)
		{
			if (manage && !await scopes.CurrentUserHasTenantWideScopeAsync())
				throw new BusinessException(UserScopeErrorCodes.AccessDenied);
			return null;
		}
		var board = await boards.GetAsync(boardId.Value);
		if (board.TenantId != CurrentTenant.Id)
			throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		if (mediumId.HasValue)
		{
			var medium = await mediums.GetAsync(mediumId.Value);
			if (medium.TenantId != CurrentTenant.Id || medium.EducationBoardId != board.Id)
				throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		}
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (organizationId.HasValue)
		{
			var organization = await organizations.GetAsync(organizationId.Value);
			if (organization.TenantId != CurrentTenant.Id || organization.EducationMediumId != mediumId)
				throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		}
		var allowed = Allows(scope, board.AcademicYearId, board.Id, mediumId, organizationId, manage);
		if (!allowed)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return board.AcademicYearId;
	}
	public static bool Allows(UserScopeContext scope, Guid? yearId, Guid? boardId, Guid? mediumId, Guid? organizationId = null, bool manage = false) =>
		!boardId.HasValue ? !manage : scope.HasAcademicYearWideScope(yearId)
			|| scope.WildcardMediumBoardIds.Contains(boardId.Value)
			|| (mediumId.HasValue && scope.WildcardMediumStageMediumIds.Contains(mediumId.Value))
			|| (organizationId.HasValue && scope.WildcardStandardMediumStageIds.Contains(organizationId.Value))
			|| (!manage && organizationId.HasValue && scope.MediumStageIds.Contains(organizationId.Value))
			|| (!manage && !organizationId.HasValue && ((!mediumId.HasValue && scope.BoardIds.Contains(boardId.Value))
				|| (mediumId.HasValue && scope.MediumIds.Contains(mediumId.Value))));

	public async Task<bool> CanManageAsync(Guid? boardId, Guid? mediumId, Guid? yearId, Guid? organizationId = null)
	{
		if (!boardId.HasValue) return await scopes.CurrentUserHasTenantWideScopeAsync();
		var scope = await scopes.GetCurrentUserScopeAsync();
		return scope.HasAcademicYearWideScope(yearId) || scope.WildcardMediumBoardIds.Contains(boardId.Value)
			|| (mediumId.HasValue && scope.WildcardMediumStageMediumIds.Contains(mediumId.Value))
			|| (organizationId.HasValue && scope.WildcardStandardMediumStageIds.Contains(organizationId.Value));
	}
}
