using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Transport;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Transport;

[ApiController]
[Authorize]
[Route("api/transport/staff")]
public class TransportStaffController(ITransportStaffAppService staffAppService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubModulePermissions.TransportStaffManagement)]
	public async Task<Result<PagedResultDto<TransportStaffDto>>> GetStaff([FromQuery] TransportStaffListRequestDto input)
	{
		var staff = await staffAppService.GetListAsync(input);
		return Result.Success(staff);
	}

	[HttpGet("options")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportStaffManagement)]
	public async Task<Result<List<TransportOptionDto>>> GetStaffOptions([FromQuery] TransportStaffOptionsRequestDto input)
	{
		var options = await staffAppService.GetOptionsAsync(input);
		return Result.Success(options);
	}

	[HttpGet("{staffId}")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportStaffManagement)]
	public async Task<Result<TransportStaffDto>> GetStaffById(Guid staffId, [FromQuery] Guid academicMediumId)
	{
		var staff = await staffAppService.GetAsync(staffId, academicMediumId);
		return Result.Success(staff);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubModulePermissions.TransportStaffManagement)]
	public async Task<Result<TransportStaffDto>> CreateStaff(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportStaffDto input)
	{
		var staff = await staffAppService.CreateAsync(academicMediumId, input);
		return Result.Success(staff);
	}

	[HttpPut("{staffId}")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportStaffManagement)]
	public async Task<Result<TransportStaffDto>> UpdateStaff(
		Guid staffId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportStaffDto input)
	{
		var staff = await staffAppService.UpdateAsync(staffId, academicMediumId, input);
		return Result.Success(staff);
	}

	[HttpPut("{staffId}/status")]
	[Authorize(ShikshaMitraSubModulePermissions.TransportStaffManagement)]
	public async Task<Result<TransportStaffDto>> ChangeStaffStatus(
		Guid staffId,
		[FromQuery] Guid academicMediumId,
		[FromBody] ChangeTransportStaffStatusDto input)
	{
		var staff = await staffAppService.ChangeStatusAsync(staffId, academicMediumId, input);
		return Result.Success(staff);
	}
}
