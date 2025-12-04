using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public interface IBuildingAppService : ICrudAppService<BuildingDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateBuildingDto>
{
}
