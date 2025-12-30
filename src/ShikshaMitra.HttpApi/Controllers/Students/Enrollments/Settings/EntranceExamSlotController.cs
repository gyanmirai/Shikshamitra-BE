using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.Settings;

/// <summary>
/// API controller for managing entrance exam slots
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/settings/entrance-exam-slots")]
public class EntranceExamSlotController(IEntranceExamSlotAppService entranceExamSlotAppService) : AbpControllerBase
{
	/// <summary>
	/// Gets a paginated list of entrance exam slots with filtering
	/// </summary>
	/// <param name="input">Filter and pagination parameters</param>
	/// <returns>Paginated list of entrance exam slots</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.View)]
	public async Task<PagedResultDto<EntranceExamSlotDto>> GetListAsync([FromQuery] GetEntranceExamSlotsInput input)
	{
		return await entranceExamSlotAppService.GetListAsync(input);
	}

	/// <summary>
	/// Gets a specific entrance exam slot by ID
	/// </summary>
	/// <param name="id">The slot identifier</param>
	/// <returns>Entrance exam slot details</returns>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.View)]
	public async Task<EntranceExamSlotDetailDto> GetAsync(Guid id)
	{
		return await entranceExamSlotAppService.GetAsync(id);
	}

	/// <summary>
	/// Creates a new entrance exam slot
	/// </summary>
	/// <param name="input">Slot creation data</param>
	/// <returns>Created entrance exam slot</returns>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.Add)]
	public async Task<EntranceExamSlotDto> CreateAsync([FromBody] CreateEntranceExamSlotDto input)
	{
		return await entranceExamSlotAppService.CreateAsync(input);
	}

	/// <summary>
	/// Updates an existing entrance exam slot
	/// </summary>
	/// <param name="id">The slot identifier</param>
	/// <param name="input">Updated slot data</param>
	/// <returns>Updated entrance exam slot</returns>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.Add)]
	public async Task<EntranceExamSlotDto> UpdateAsync(Guid id, [FromBody] UpdateEntranceExamSlotDto input)
	{
		return await entranceExamSlotAppService.UpdateAsync(id, input);
	}

	/// <summary>
	/// Deletes an entrance exam slot
	/// </summary>
	/// <param name="id">The slot identifier</param>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		await entranceExamSlotAppService.DeleteAsync(id);
	}

	/// <summary>
	/// Gets available entrance exam slots for student scheduling
	/// </summary>
	/// <param name="input">Filter parameters</param>
	/// <returns>Paginated list of available slots</returns>
	[HttpGet("available")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsEntranceExamSlots.View)]
	public async Task<PagedResultDto<AvailableExamSlotDto>> GetAvailableSlotsAsync([FromQuery] GetAvailableExamSlotsInput input)
	{
		return await entranceExamSlotAppService.GetAvailableSlotsAsync(input);
	}
}
