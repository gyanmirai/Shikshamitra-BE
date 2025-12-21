using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// API Controller for managing earning components.
/// Implements both OpenAPI contract-compliant endpoints and legacy endpoints for backward compatibility.
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/earning-components")]
[Authorize]
public class EarningComponentController : AbpControllerBase
{
	private readonly IEarningComponentAppService _earningComponentAppService;

	public EarningComponentController(IEarningComponentAppService earningComponentAppService)
	{
		_earningComponentAppService = earningComponentAppService;
	}

	#region OpenAPI Contract-Compliant Endpoints

	/// <summary>
	/// Get list of all earning components (OpenAPI contract: ComponentListResponse)
	/// Returns {total, data} format as per OpenAPI specification
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(ComponentListResponseDto), 200)]
	public virtual async Task<ComponentListResponseDto> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		var result = await _earningComponentAppService.GetListAsync(input);
		return EarningComponentOpenApiMapper.ToListResponse(result);
	}

	/// <summary>
	/// Get a specific earning component by ID (OpenAPI contract: ComponentResponse)
	/// </summary>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(ComponentResponseDto), 200)]
	public virtual async Task<ComponentResponseDto> GetAsync(Guid id)
	{
		var result = await _earningComponentAppService.GetAsync(id);
		return EarningComponentOpenApiMapper.ToComponentResponse(result);
	}

	/// <summary>
	/// Create a new earning component (OpenAPI contract: accepts ComponentRequest, returns BasicSuccess with 201)
	/// </summary>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 201)]
	public virtual async Task<IActionResult> CreateAsync([FromBody] ComponentRequestDto input)
	{
		var createDto = EarningComponentOpenApiMapper.ToCreateDto(input);
		await _earningComponentAppService.CreateAsync(createDto);
		
		return StatusCode(201, ApiSuccessResponseDto.SuccessResult("Component created successfully"));
	}

	/// <summary>
	/// Update an existing earning component (OpenAPI contract: accepts ComponentRequest, returns BasicSuccess)
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> UpdateAsync(Guid id, [FromBody] ComponentRequestDto input)
	{
		// Fetch existing component to preserve display order (not in OpenAPI contract)
		var existingComponent = await _earningComponentAppService.GetAsync(id);
		var updateDto = EarningComponentOpenApiMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
		await _earningComponentAppService.UpdateAsync(id, updateDto);
		
		return ApiSuccessResponseDto.SuccessResult("Component updated successfully");
	}

	/// <summary>
	/// Delete an earning component (OpenAPI contract: returns BasicSuccess)
	/// </summary>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Delete)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> DeleteAsync(Guid id)
	{
		await _earningComponentAppService.DeleteAsync(id);
		return ApiSuccessResponseDto.SuccessResult("Component deleted successfully");
	}

	#endregion

	#region Additional Endpoints (Not in OpenAPI Contract)

	/// <summary>
	/// Get all active earning components with pagination support
	/// </summary>
	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(ComponentListResponseDto), 200)]
	public virtual async Task<ComponentListResponseDto> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)
	{
		var result = await _earningComponentAppService.GetAllActiveAsync(input);
		return EarningComponentOpenApiMapper.ToListResponse(result);
	}

	/// <summary>
	/// Activate an earning component
	/// </summary>
	[HttpPost("{id}/activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> ActivateAsync(Guid id)
	{
		await _earningComponentAppService.ActivateAsync(id);
		return ApiSuccessResponseDto.SuccessResult("Component activated successfully");
	}

	/// <summary>
	/// Deactivate an earning component
	/// </summary>
	[HttpPost("{id}/deactivate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> DeactivateAsync(Guid id)
	{
		await _earningComponentAppService.DeactivateAsync(id);
		return ApiSuccessResponseDto.SuccessResult("Component deactivated successfully");
	}

	#endregion

	#region Legacy Endpoints (For Backward Compatibility)

	/// <summary>
	/// [Legacy] Get list of all earning components with ABP standard response format
	/// </summary>
	[HttpGet("legacy")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(PagedResultDto<EarningComponentDto>), 200)]
	public virtual Task<PagedResultDto<EarningComponentDto>> GetListLegacyAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return _earningComponentAppService.GetListAsync(input);
	}

	/// <summary>
	/// [Legacy] Get a specific earning component by ID with domain DTO response
	/// </summary>
	[HttpGet("legacy/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(EarningComponentDto), 200)]
	public virtual Task<EarningComponentDto> GetLegacyAsync(Guid id)
	{
		return _earningComponentAppService.GetAsync(id);
	}

	/// <summary>
	/// [Legacy] Create a new earning component with domain DTO
	/// </summary>
	[HttpPost("legacy")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(EarningComponentDto), 200)]
	public virtual Task<EarningComponentDto> CreateLegacyAsync([FromBody] CreateEarningComponentDto input)
	{
		return _earningComponentAppService.CreateAsync(input);
	}

	/// <summary>
	/// [Legacy] Update an existing earning component with domain DTO
	/// </summary>
	[HttpPut("legacy/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(EarningComponentDto), 200)]
	public virtual Task<EarningComponentDto> UpdateLegacyAsync(Guid id, [FromBody] UpdateEarningComponentDto input)
	{
		return _earningComponentAppService.UpdateAsync(id, input);
	}

	#endregion
}
