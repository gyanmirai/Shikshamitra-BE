using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records.Settings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records.Settings;

[ApiController]
[Authorize]
[Route("api/students/records/house-settings")]
public class HouseSettingsController(
	IHouseSettingsAppService houseSettingsAppService
) : AbpController
{
	[HttpPost("/api/students/records/academic-mediums/{academicMediumId}/house-settings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add)]
	public async Task<Result> CreateHouseSettings(Guid academicMediumId, [FromBody] CreateUpdateHouseSettingsDto createHouseSettingsDto)
	{
		await houseSettingsAppService.CreateHouseSettingsAsync(academicMediumId, createHouseSettingsDto);
		return Result.Success();
	}

	[HttpGet("{houseSettingsId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.View)]
	public async Task<Result<HouseSettingsDto>> GetHouseSettings(Guid houseSettingsId)
	{
		var houseSettings = await houseSettingsAppService.GetHouseSettingsAsync(houseSettingsId);
		return Result.Success(houseSettings);
	}

	[HttpPut("{houseSettingsId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add)]
	public async Task<Result<HouseSettingsDto>> UpdateHouseSettings(
		Guid houseSettingsId,
		[FromBody] CreateUpdateHouseSettingsDto updateHouseSettingsDto)
	{
		var updatedHouseSettings = await houseSettingsAppService.UpdateHouseSettingsAsync(houseSettingsId, updateHouseSettingsDto);
		return Result.Success(updatedHouseSettings);
	}

	[HttpGet("/api/students/records/academic-mediums/{academicMediumId}/house-settings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.View)]
	public async Task<Result<List<HouseSettingsDto>>> GetHouseSettingsByAcademicMedium(Guid academicMediumId)
	{
		var houseSettings = await houseSettingsAppService.GetHouseSettingsByAcademicMediumAsync(academicMediumId);
		return Result.Success(houseSettings);
	}

	[HttpDelete("{houseSettingsId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Delete)]
	public async Task<Result> DeleteHouseSettings(Guid houseSettingsId)
	{
		await houseSettingsAppService.DeleteHouseSettingsAsync(houseSettingsId);
		return Result.Success();
	}
}