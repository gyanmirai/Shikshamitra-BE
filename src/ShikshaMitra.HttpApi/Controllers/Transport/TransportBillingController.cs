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
[Route("api/transport")]
public class TransportBillingController(ITransportBillingAppService billingAppService) : AbpController
{
	[HttpGet("zones-areas")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_ZoneAreaConfiguration.View)]
	public async Task<Result<List<TransportZoneAreaDto>>> GetZoneAreas([FromQuery] Guid academicMediumId)
	{
		var rows = await billingAppService.GetZoneAreasAsync(academicMediumId);
		return Result.Success(rows);
	}

	[HttpPost("zones-areas")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_ZoneAreaConfiguration.Add)]
	public async Task<Result<TransportZoneAreaDto>> CreateZoneArea(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportZoneAreaDto input)
	{
		var row = await billingAppService.CreateZoneAreaAsync(academicMediumId, input);
		return Result.Success(row);
	}

	[HttpPut("zones-areas/{zoneAreaId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_ZoneAreaConfiguration.Add)]
	public async Task<Result<TransportZoneAreaDto>> UpdateZoneArea(
		Guid zoneAreaId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportZoneAreaDto input)
	{
		var row = await billingAppService.UpdateZoneAreaAsync(zoneAreaId, academicMediumId, input);
		return Result.Success(row);
	}

	[HttpDelete("zones-areas/{zoneAreaId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_ZoneAreaConfiguration.Delete)]
	public async Task<IActionResult> DeleteZoneArea(Guid zoneAreaId, [FromQuery] Guid academicMediumId)
	{
		await billingAppService.DeleteZoneAreaAsync(zoneAreaId, academicMediumId);
		return NoContent();
	}

	[HttpGet("fee-frequency-options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.View)]
	public async Task<Result<List<TransportOptionDto>>> GetFeeFrequencyOptions()
	{
		var rows = await billingAppService.GetFeeFrequencyOptionsAsync();
		return Result.Success(rows);
	}

	[HttpGet("fee-configuration")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.View)]
	public async Task<Result<TransportFeeConfigurationDto?>> GetFeeConfiguration([FromQuery] Guid academicMediumId)
	{
		var configuration = await billingAppService.GetFeeConfigurationAsync(academicMediumId);
		return Result.Success(configuration);
	}

	[HttpPost("fee-configuration")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.Add)]
	public async Task<Result<TransportFeeConfigurationDto>> CreateFeeConfiguration(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportFeeConfigurationDto input)
	{
		var configuration = await billingAppService.CreateFeeConfigurationAsync(academicMediumId, input);
		return Result.Success(configuration);
	}

	[HttpPut("fee-configuration/{configurationId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.Add)]
	public async Task<Result<TransportFeeConfigurationDto>> UpdateFeeConfiguration(
		Guid configurationId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateTransportFeeConfigurationDto input)
	{
		var configuration = await billingAppService.UpdateFeeConfigurationAsync(configurationId, academicMediumId, input);
		return Result.Success(configuration);
	}

	[HttpPost("fees/runs/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.View)]
	public async Task<Result<TransportFeeRunPreviewDto>> PreviewFeeRun(
		[FromQuery] Guid academicMediumId,
		[FromBody] TransportFeeRunRequestDto input)
	{
		var preview = await billingAppService.PreviewRunAsync(academicMediumId, input);
		return Result.Success(preview);
	}

	[HttpPost("fees/runs")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.Add)]
	public async Task<Result<TransportFeeRunDto>> CreateFeeRun(
		[FromQuery] Guid academicMediumId,
		[FromBody] TransportFeeRunRequestDto input)
	{
		var run = await billingAppService.CreateRunAsync(academicMediumId, input);
		return Result.Success(run);
	}

	[HttpGet("fees/runs")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.View)]
	public async Task<Result<PagedResultDto<TransportFeeRunDto>>> GetFeeRuns(
		[FromQuery] TransportFeeRunListRequestDto input)
	{
		var runs = await billingAppService.GetRunsAsync(input);
		return Result.Success(runs);
	}

	[HttpGet("fees/runs/{runId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.View)]
	public async Task<Result<TransportFeeRunDto>> GetFeeRun(Guid runId, [FromQuery] Guid academicMediumId)
	{
		var run = await billingAppService.GetRunAsync(runId, academicMediumId);
		return Result.Success(run);
	}

	[HttpPost("fees/runs/{runId}/post-finance")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.Add)]
	public async Task<Result<TransportFeeFinancePostResultDto>> PostFeeRunToFinance(
		Guid runId,
		[FromQuery] Guid academicMediumId)
	{
		var result = await billingAppService.PostRunToFinanceAsync(runId, academicMediumId);
		return Result.Success(result);
	}

	[HttpGet("fees/dues")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.View)]
	public async Task<Result<PagedResultDto<TransportFeeDueDto>>> GetDues(
		[FromQuery] TransportFeeDueListRequestDto input)
	{
		var dues = await billingAppService.GetDuesAsync(input);
		return Result.Success(dues);
	}

	[HttpGet("members/{memberId}/dues")]
	[Authorize(ShikshaMitraSubmodulePermissions.Transport.Settings_FeeConfiguration.View)]
	public async Task<Result<PagedResultDto<TransportFeeDueDto>>> GetMemberDues(
		Guid memberId,
		[FromQuery] TransportMemberDueListRequestDto input)
	{
		var dues = await billingAppService.GetMemberDuesAsync(memberId, input);
		return Result.Success(dues);
	}
}
