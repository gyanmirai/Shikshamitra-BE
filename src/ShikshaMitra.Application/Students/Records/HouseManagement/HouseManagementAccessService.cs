using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records.HouseManagement;

[RemoteService(false)]
public class HouseManagementAccessService(
	StudentRecordsActorService actor,
	StudentScopeAccessService studentScope,
	IUserScopeAccessService scopes,
	IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationBoard, Guid> boards)
	: ApplicationService, ITransientDependency
{
	public async Task<HouseManagementContext> GetAsync(Guid mediumId, bool manage = false)
	{
		await actor.EnsureActiveAsync();
		if (mediumId == Guid.Empty)
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
		var query =
			from m in await mediums.GetQueryableAsync()
			join b in await boards.GetQueryableAsync() on m.EducationBoardId equals b.Id
			where m.Id == mediumId && m.TenantId == CurrentTenant.Id && b.TenantId == CurrentTenant.Id
			select new HouseManagementContext(b.AcademicYearId, b.Id, m.Id);
		var context = await AsyncExecuter.SingleOrDefaultAsync(query) ?? throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		await studentScope.EnsureCanAccessMediumAsync(mediumId, "HouseManagement");
		context.CanManage = StudentRecordScope.HasAccess(context, await scopes.GetCurrentUserScopeAsync());
		if (manage && !context.CanManage)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return context;
	}
}

public sealed record HouseManagementContext(Guid YearId, Guid BoardId, Guid MediumId) : IUserScopedEntity
{
	public bool CanManage { get; set; }
	public Guid? AcademicYearId => YearId;
	public Guid? EducationBoardId => BoardId;
	public Guid? EducationMediumId => MediumId;
	public Guid? EducationMediumStageId => null;
	public Guid? StandardId => null;
	public Guid? DivisionId => null;
}
