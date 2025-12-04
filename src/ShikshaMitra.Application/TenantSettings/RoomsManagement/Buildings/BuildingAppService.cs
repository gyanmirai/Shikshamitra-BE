using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class BuildingAppService(IRepository<Building, Guid> repository)
	: CrudAppService<Building, BuildingDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateBuildingDto>(repository),
	IBuildingAppService
{
}
