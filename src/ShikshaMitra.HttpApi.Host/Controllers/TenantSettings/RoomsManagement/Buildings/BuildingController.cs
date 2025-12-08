using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings.RoomsManagement.Buildings;

[Authorize]
[Route("institute-setup/buildings")]
[ApiController]
public class BuildingController(IBuildingAppService buildingAppService) : AbpController
{
	[HttpGet]
	[ProducesResponseType(typeof(List<BuildingDto>), (int)HttpStatusCode.OK)]
	public async Task<List<BuildingDto>> GetAllAsync()
	{
		return await buildingAppService.GetListAsync();
	}

	[HttpGet("{id}")]
	[ProducesResponseType(typeof(BuildingDto), (int)HttpStatusCode.OK)]
	[ProducesResponseType((int)HttpStatusCode.NotFound)]
	public async Task<BuildingDto> GetAsync(Guid id)
	{
		return await buildingAppService.GetAsync(id);
	}

	[HttpPost]
	[ProducesResponseType(typeof(BuildingDto), (int)HttpStatusCode.Created)]
	[ProducesResponseType((int)HttpStatusCode.BadRequest)]
	public async Task<ActionResult<BuildingDto>> CreateAsync([FromBody] CreateUpdateBuildingDto input)
	{
		var result = await buildingAppService.CreateAsync(input);
		return StatusCode((int)HttpStatusCode.Created, result);
	}

	[HttpPut("{id}")]
	[ProducesResponseType(typeof(BuildingDto), (int)HttpStatusCode.OK)]
	[ProducesResponseType((int)HttpStatusCode.NotFound)]
	public async Task<BuildingDto> UpdateAsync(Guid id, [FromBody] CreateUpdateBuildingDto input)
	{
		return await buildingAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	[ProducesResponseType((int)HttpStatusCode.NoContent)]
	[ProducesResponseType((int)HttpStatusCode.NotFound)]
	public async Task<IActionResult> DeleteAsync(Guid id)
	{
		await buildingAppService.DeleteAsync(id);
		return NoContent();
	}
}
