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
/// API Controller for managing deduction components.
/// Implements OpenAPI contract-compliant endpoints.
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/deduction-components")]
[Authorize]
public class DeductionComponentController : AbpControllerBase
{
	private readonly IDeductionComponentAppService _deductionComponentAppService;

	public DeductionComponentController(IDeductionComponentAppService deductionComponentAppService)
	{
		_deductionComponentAppService = deductionComponentAppService;
	}

	/// <summary>
	/// Get list of all deduction components (OpenAPI contract: ComponentListResponse)
	/// Returns {total, data} format as per OpenAPI specification
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(ComponentListResponseDto), 200)]
	public virtual async Task<ComponentListResponseDto> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		var result = await _deductionComponentAppService.GetListAsync(input);
		return DeductionComponentOpenApiMapper.ToListResponse(result);
	}

	/// <summary>
	/// Get a specific deduction component by ID (OpenAPI contract: ComponentResponse)
	/// </summary>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(ComponentResponseDto), 200)]
	public virtual async Task<ComponentResponseDto> GetAsync(Guid id)
	{
		var result = await _deductionComponentAppService.GetAsync(id);
		return DeductionComponentOpenApiMapper.ToComponentResponse(result);
	}

	/// <summary>
	/// Create a new deduction component (OpenAPI contract: accepts ComponentRequest, returns BasicSuccess with 201)
	/// </summary>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 201)]
	public virtual async Task<IActionResult> CreateAsync([FromBody] ComponentRequestDto input)
	{
		var createDto = DeductionComponentOpenApiMapper.ToCreateDto(input);
		await _deductionComponentAppService.CreateAsync(createDto);
		
		return StatusCode(201, ApiSuccessResponseDto.SuccessResult("Component created successfully"));
	}

	/// <summary>
	/// Update an existing deduction component (OpenAPI contract: accepts ComponentRequest, returns BasicSuccess)
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> UpdateAsync(Guid id, [FromBody] ComponentRequestDto input)
	{
		// Fetch existing component to preserve display order (not in OpenAPI contract)
		var existingComponent = await _deductionComponentAppService.GetAsync(id);
		var updateDto = DeductionComponentOpenApiMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
		await _deductionComponentAppService.UpdateAsync(id, updateDto);
		
		return ApiSuccessResponseDto.SuccessResult("Component updated successfully");
	}

	/// <summary>
	/// Delete a deduction component (OpenAPI contract: returns BasicSuccess)
	/// </summary>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Delete)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> DeleteAsync(Guid id)
	{
		await _deductionComponentAppService.DeleteAsync(id);
		return ApiSuccessResponseDto.SuccessResult("Component deleted successfully");
	}

	/// <summary>
	/// Get all active deduction components with pagination support
	/// </summary>
	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(ComponentListResponseDto), 200)]
	public virtual async Task<ComponentListResponseDto> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)
	{
		var result = await _deductionComponentAppService.GetAllActiveAsync(input);
		return DeductionComponentOpenApiMapper.ToListResponse(result);
	}

	/// <summary>
	/// Activate a deduction component
	/// </summary>
	[HttpPost("{id}/activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> ActivateAsync(Guid id)
	{
		await _deductionComponentAppService.ActivateAsync(id);
		return ApiSuccessResponseDto.SuccessResult("Component activated successfully");
	}

	/// <summary>
	/// Deactivate a deduction component
	/// </summary>
	[HttpPost("{id}/deactivate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> DeactivateAsync(Guid id)
	{
		await _deductionComponentAppService.DeactivateAsync(id);
		return ApiSuccessResponseDto.SuccessResult("Component deactivated successfully");
	}
}
