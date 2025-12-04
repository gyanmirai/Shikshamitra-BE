using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings.RoomsManagement.Buildings;

[Authorize]
[Route("api/institute-setup/rooms-management/buildings")]
public class BuildingController(IBuildingAppService buildingAppService) : AbpController
{
	[HttpGet]
	public async Task<PagedResultDto<BuildingDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return await buildingAppService.GetListAsync(input);
	}

	[HttpGet("{id}")]
	public async Task<BuildingDto> GetAsync(Guid id)
	{
		return await buildingAppService.GetAsync(id);
	}

	[HttpPost]
	public async Task<BuildingDto> CreateAsync([FromBody] CreateUpdateBuildingDto input)
	{
		return await buildingAppService.CreateAsync(input);
	}

	[HttpPut("{id}")]
	public async Task<BuildingDto> UpdateAsync(Guid id, [FromBody] CreateUpdateBuildingDto input)
	{
		return await buildingAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	public async Task DeleteAsync(Guid id)
	{
		await buildingAppService.DeleteAsync(id);
	}
}
