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
[Route("api/transport/applications")]
public class TransportApplicationsController(ITransportApplicationAppService applicationAppService) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<PagedResultDto<TransportApplicationDto>>> GetApplications(
		[FromQuery] TransportApplicationListRequestDto input)
	{
		var applications = await applicationAppService.GetListAsync(input);
		return Result.Success(applications);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<TransportApplicationDto>> CreateApplication(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportApplicationDto input)
	{
		var application = await applicationAppService.CreateAsync(academicMediumId, input);
		return Result.Success(application);
	}

	[HttpGet("{applicationId}")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<TransportApplicationDto>> GetApplication(Guid applicationId, [FromQuery] Guid academicMediumId)
	{
		var application = await applicationAppService.GetAsync(applicationId, academicMediumId);
		return Result.Success(application);
	}

	[HttpPut("{applicationId}")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<TransportApplicationDto>> UpdateApplication(
		Guid applicationId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportApplicationDto input)
	{
		var application = await applicationAppService.UpdateAsync(applicationId, academicMediumId, input);
		return Result.Success(application);
	}

	[HttpGet("{applicationId}/suggested-routes")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<List<TransportSuggestedRouteDto>>> GetSuggestedRoutes(
		Guid applicationId,
		[FromQuery] Guid academicMediumId)
	{
		var routes = await applicationAppService.GetSuggestedRoutesAsync(applicationId, academicMediumId);
		return Result.Success(routes);
	}

	[HttpPut("{applicationId}/route-stop")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<TransportApplicationDto>> AssignRouteStop(
		Guid applicationId,
		[FromQuery] Guid academicMediumId,
		[FromBody] AssignTransportApplicationRouteStopDto input)
	{
		var application = await applicationAppService.AssignRouteStopAsync(applicationId, academicMediumId, input);
		return Result.Success(application);
	}

	[HttpPut("{applicationId}/status")]
	[Authorize(ShikshaMitraSubModulePermissions.ApplicationManagement)]
	public async Task<Result<TransportApplicationDto>> ChangeStatus(
		Guid applicationId,
		[FromQuery] Guid academicMediumId,
		[FromBody] ChangeTransportApplicationStatusDto input)
	{
		var application = await applicationAppService.ChangeStatusAsync(applicationId, academicMediumId, input);
		return Result.Success(application);
	}
}
