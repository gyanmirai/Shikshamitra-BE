using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

	#region OpenAPI Contract-Compliant Endpoints

	/// <summary>
	/// Get list of all deduction components (OpenAPI contract: ComponentListResponse)
	/// Returns {total, data} format as per OpenAPI specification
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(ComponentListResponseDto), 200)]
	public virtual async Task<ComponentListResponseDto> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		try
		{
			var result = await _deductionComponentAppService.GetListAsync(input);
			return DeductionComponentOpenApiMapper.ToListResponse(result);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error getting list of deduction components");
			throw;
		}
	}

	/// <summary>
	/// Get all active deduction components with pagination support
	/// Note: While this endpoint uses similar response format, it filters only active components.
	/// Consider adding this to OpenAPI specification if frontend requires it.
	/// </summary>
	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(ComponentListResponseDto), 200)]
	public virtual async Task<ComponentListResponseDto> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)
	{
		try
		{
			var result = await _deductionComponentAppService.GetAllActiveAsync(input);
			return DeductionComponentOpenApiMapper.ToListResponse(result);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error getting list of active deduction components");
			throw;
		}
	}

	/// <summary>
	/// Get a specific deduction component by ID (OpenAPI contract: ComponentResponse)
	/// </summary>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	[ProducesResponseType(typeof(ComponentResponseDto), 200)]
	public virtual async Task<ComponentResponseDto> GetAsync(Guid id)
	{
		try
		{
			var result = await _deductionComponentAppService.GetAsync(id);
			return DeductionComponentOpenApiMapper.ToComponentResponse(result);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error getting deduction component with ID: {Id}", id);
			throw;
		}
	}

	/// <summary>
	/// Create a new deduction component (OpenAPI contract: accepts ComponentRequest, returns BasicSuccess with 201)
	/// Returns the ID of the created component for reference
	/// </summary>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessWithIdResponseDto), 201)]
	public virtual async Task<IActionResult> CreateAsync([FromBody] ComponentRequestDto input)
	{
		try
		{
			var createDto = DeductionComponentOpenApiMapper.ToCreateDto(input);
			var createdComponent = await _deductionComponentAppService.CreateAsync(createDto);
			
			return StatusCode(201, ApiSuccessWithIdResponseDto.SuccessResultWithId(
				"Component created successfully", 
				createdComponent.Id));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error creating deduction component");
			throw;
		}
	}

	/// <summary>
	/// Update an existing deduction component (OpenAPI contract: accepts ComponentRequest, returns BasicSuccess)
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> UpdateAsync(Guid id, [FromBody] ComponentRequestDto input)
	{
		try
		{
			// Fetch existing component to preserve display order (not in OpenAPI contract)
			var existingComponent = await _deductionComponentAppService.GetAsync(id);
			var updateDto = DeductionComponentOpenApiMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
			await _deductionComponentAppService.UpdateAsync(id, updateDto);
			
			return ApiSuccessResponseDto.SuccessResult("Component updated successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error updating deduction component with ID: {Id}", id);
			throw;
		}
	}

	/// <summary>
	/// Delete a deduction component (OpenAPI contract: returns BasicSuccess)
	/// </summary>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Delete)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> DeleteAsync(Guid id)
	{
		try
		{
			await _deductionComponentAppService.DeleteAsync(id);
			return ApiSuccessResponseDto.SuccessResult("Component deleted successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error deleting deduction component with ID: {Id}", id);
			throw;
		}
	}

	#endregion

	#region Additional Endpoints (Not in OpenAPI Contract)

	/// <summary>
	/// Activate a deduction component
	/// </summary>
	[HttpPost("{id}/activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> ActivateAsync(Guid id)
	{
		try
		{
			await _deductionComponentAppService.ActivateAsync(id);
			return ApiSuccessResponseDto.SuccessResult("Component activated successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error activating deduction component with ID: {Id}", id);
			throw;
		}
	}

	/// <summary>
	/// Deactivate a deduction component
	/// </summary>
	[HttpPost("{id}/deactivate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> DeactivateAsync(Guid id)
	{
		try
		{
			await _deductionComponentAppService.DeactivateAsync(id);
			return ApiSuccessResponseDto.SuccessResult("Component deactivated successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error deactivating deduction component with ID: {Id}", id);
			throw;
		}
	}

	#endregion
}
