using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.InitialSetup;

[RemoteService(false)]
public class InitialSetupStructureReader(
	IRepository<AcademicYear, Guid> years, IRepository<Division, Guid> divisions,
	IRepository<Standard, Guid> standards, IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards) : ShikshaMitraAppService
{
	public async Task<bool> IsReadyAsync(Guid tenantId, Guid? yearId = null, Guid? divisionId = null)
	{
		if (!CurrentUser.IsAuthenticated || CurrentUser.TenantId != tenantId || CurrentTenant.Id != tenantId)
			throw new AbpAuthorizationException();
		var divisionQuery = (await divisions.GetQueryableAsync()).Where(x => x.TenantId == tenantId && (!divisionId.HasValue || x.Id == divisionId) && x.Strength > 0);
		var query = from division in divisionQuery
			join standard in (await standards.GetQueryableAsync()).Where(x => x.TenantId == tenantId) on division.StandardId equals standard.Id
			join stage in (await stages.GetQueryableAsync()).Where(x => x.TenantId == tenantId) on standard.EducationMediumStageId equals stage.Id
			join medium in (await mediums.GetQueryableAsync()).Where(x => x.TenantId == tenantId) on stage.EducationMediumId equals medium.Id
			join board in (await boards.GetQueryableAsync()).Where(x => x.TenantId == tenantId && (!yearId.HasValue || x.AcademicYearId == yearId)) on medium.EducationBoardId equals board.Id
			join year in (await years.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.EndDate > x.StartDate) on board.AcademicYearId equals year.Id
			select division.Id;
		return await AsyncExecuter.AnyAsync(query);
	}
}
