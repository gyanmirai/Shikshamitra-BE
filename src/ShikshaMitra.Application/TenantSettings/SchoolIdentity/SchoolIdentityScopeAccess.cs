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
	IUserScopeAccessService scopes) : ApplicationService, ITransientDependency
{
	public async Task<Guid?> EnsureAsync(Guid? boardId, Guid? mediumId, bool manage = false)
	{
		if (!CurrentTenant.Id.HasValue || (mediumId.HasValue && !boardId.HasValue))
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
		var allowed = scope.HasAcademicYearWideScope(board.AcademicYearId)
			|| scope.WildcardMediumBoardIds.Contains(board.Id)
			|| (mediumId.HasValue && scope.WildcardMediumStageMediumIds.Contains(mediumId.Value))
			|| (!manage && ((!mediumId.HasValue && scope.BoardIds.Contains(board.Id))
				|| (mediumId.HasValue && scope.MediumIds.Contains(mediumId.Value))));
		if (!allowed)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return board.AcademicYearId;
	}

	public async Task<bool> CanManageAsync(Guid? boardId, Guid? mediumId, Guid? yearId)
	{
		if (!boardId.HasValue) return await scopes.CurrentUserHasTenantWideScopeAsync();
		var scope = await scopes.GetCurrentUserScopeAsync();
		return scope.HasAcademicYearWideScope(yearId) || scope.WildcardMediumBoardIds.Contains(boardId.Value)
			|| (mediumId.HasValue && scope.WildcardMediumStageMediumIds.Contains(mediumId.Value));
	}
}
