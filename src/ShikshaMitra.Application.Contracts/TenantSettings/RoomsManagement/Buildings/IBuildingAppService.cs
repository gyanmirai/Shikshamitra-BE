using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public interface IBuildingAppService
{
	Task<List<BuildingDto>> GetListAsync();
	Task<BuildingDto> GetAsync(Guid id);
	Task<BuildingDto> CreateAsync(CreateUpdateBuildingDto input);
	Task<BuildingDto> UpdateAsync(Guid id, CreateUpdateBuildingDto input);
	Task DeleteAsync(Guid id);
}
