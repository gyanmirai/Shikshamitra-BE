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
[Route("api/transport/routes")]
public class TransportRoutesController(ITransportRouteAppService routeAppService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<PagedResultDto<TransportRouteDto>>> GetRoutes([FromQuery] TransportRouteListRequestDto input)
	{
		var routes = await routeAppService.GetListAsync(input);
		return Result.Success(routes);
	}

	[HttpGet("{routeId}")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<TransportRouteDto>> GetRoute(Guid routeId, [FromQuery] Guid academicMediumId)
	{
		var route = await routeAppService.GetAsync(routeId, academicMediumId);
		return Result.Success(route);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<TransportRouteDto>> CreateRoute(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportRouteDto input)
	{
		var route = await routeAppService.CreateAsync(academicMediumId, input);
		return Result.Success(route);
	}

	[HttpPut("{routeId}")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<TransportRouteDto>> UpdateRoute(
		Guid routeId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportRouteDto input)
	{
		var route = await routeAppService.UpdateAsync(routeId, academicMediumId, input);
		return Result.Success(route);
	}

	[HttpGet("{routeId}/stops")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<List<TransportRouteStopDto>>> GetRouteStops(Guid routeId, [FromQuery] Guid academicMediumId)
	{
		var stops = await routeAppService.GetStopsAsync(routeId, academicMediumId);
		return Result.Success(stops);
	}

	[HttpPost("{routeId}/stops")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<List<TransportRouteStopDto>>> AssignRouteStops(
		Guid routeId,
		[FromQuery] Guid academicMediumId,
		[FromBody] AssignTransportRouteStopsDto input)
	{
		var stops = await routeAppService.AssignStopsAsync(routeId, academicMediumId, input);
		return Result.Success(stops);
	}

	[HttpPut("{routeId}/status")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<TransportRouteDto>> ChangeRouteStatus(
		Guid routeId,
		[FromQuery] Guid academicMediumId,
		[FromBody] ChangeTransportStatusDto input)
	{
		var route = await routeAppService.ChangeStatusAsync(routeId, academicMediumId, input);
		return Result.Success(route);
	}

	[HttpGet("{routeId}/vehicle-assignment")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<TransportRouteAssignmentDto?>> GetVehicleAssignment(Guid routeId, [FromQuery] Guid academicMediumId)
	{
		var assignment = await routeAppService.GetVehicleAssignmentAsync(routeId, academicMediumId);
		return Result.Success(assignment);
	}

	[HttpPost("{routeId}/vehicle-assignment")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<TransportRouteAssignmentDto>> AssignVehicle(
		Guid routeId,
		[FromQuery] Guid academicMediumId,
		[FromBody] AssignTransportRouteVehicleDto input)
	{
		var assignment = await routeAppService.AssignVehicleAsync(routeId, academicMediumId, input);
		return Result.Success(assignment);
	}

	[HttpDelete("{routeId}/vehicle-assignment")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<IActionResult> DeleteVehicleAssignment(Guid routeId, [FromQuery] Guid academicMediumId)
	{
		await routeAppService.DeleteVehicleAssignmentAsync(routeId, academicMediumId);
		return NoContent();
	}

	[HttpGet("{routeId}/vehicle-history")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<PagedResultDto<TransportVehicleHistoryRowDto>>> GetVehicleHistory(
		Guid routeId,
		[FromQuery] TransportVehicleMaintenanceListRequestDto input)
	{
		var history = await routeAppService.GetVehicleHistoryAsync(routeId, input);
		return Result.Success(history);
	}

	[HttpGet("{routeId}/driver-conductor-history")]
	[Authorize(ShikshaMitraSubModulePermissions.RouteManagement)]
	public async Task<Result<PagedResultDto<TransportVehicleHistoryRowDto>>> GetDriverConductorHistory(
		Guid routeId,
		[FromQuery] TransportVehicleMaintenanceListRequestDto input)
	{
		var history = await routeAppService.GetDriverConductorHistoryAsync(routeId, input);
		return Result.Success(history);
	}
}
