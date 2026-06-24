using System;
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
[Route("api/transport/stops")]
public class TransportStopsController(ITransportStopAppService stopAppService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubModulePermissions.StopManagement)]
	public async Task<Result<PagedResultDto<TransportStopDto>>> GetStops([FromQuery] TransportStopListRequestDto input)
	{
		var stops = await stopAppService.GetListAsync(input);
		return Result.Success(stops);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubModulePermissions.StopManagement)]
	public async Task<Result<TransportStopDto>> CreateStop(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportStopDto input)
	{
		var stop = await stopAppService.CreateAsync(academicMediumId, input);
		return Result.Success(stop);
	}

	[HttpPut("{stopId}")]
	[Authorize(ShikshaMitraSubModulePermissions.StopManagement)]
	public async Task<Result<TransportStopDto>> UpdateStop(
		Guid stopId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportStopDto input)
	{
		var stop = await stopAppService.UpdateAsync(stopId, academicMediumId, input);
		return Result.Success(stop);
	}

	[HttpPut("{stopId}/status")]
	[Authorize(ShikshaMitraSubModulePermissions.StopManagement)]
	public async Task<Result<TransportStopDto>> ChangeStopStatus(
		Guid stopId,
		[FromQuery] Guid academicMediumId,
		[FromBody] ChangeTransportStatusDto input)
	{
		var stop = await stopAppService.ChangeStatusAsync(stopId, academicMediumId, input);
		return Result.Success(stop);
	}
}
