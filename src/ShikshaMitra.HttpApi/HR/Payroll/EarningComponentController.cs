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
/// API Controller for managing earning components.
/// Implements OpenAPI contract-compliant endpoints.
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
		try
		{
			var result = await _earningComponentAppService.GetListAsync(input);
			return EarningComponentOpenApiMapper.ToListResponse(result);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error getting list of earning components");
			throw;
		}
	}

	/// <summary>
	/// Get all active earning components with pagination support
	/// Note: While this endpoint uses similar response format, it filters only active components.
	/// Consider adding this to OpenAPI specification if frontend requires it.
	/// </summary>
	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(ComponentListResponseDto), 200)]
	public virtual async Task<ComponentListResponseDto> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)
	{
		try
		{
			var result = await _earningComponentAppService.GetAllActiveAsync(input);
			return EarningComponentOpenApiMapper.ToListResponse(result);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error getting list of active earning components");
			throw;
		}
	}

	/// <summary>
	/// Get a specific earning component by ID (OpenAPI contract: ComponentResponse)
	/// </summary>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(ComponentResponseDto), 200)]
	public virtual async Task<ComponentResponseDto> GetAsync(Guid id)
	{
		try
		{
			var result = await _earningComponentAppService.GetAsync(id);
			return EarningComponentOpenApiMapper.ToComponentResponse(result);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error getting earning component with ID: {Id}", id);
			throw;
		}
	}

	/// <summary>
	/// Create a new earning component (OpenAPI contract: accepts ComponentRequest, returns BasicSuccess with 201)
	/// Returns the ID of the created component for reference
	/// </summary>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessWithIdResponseDto), 201)]
	public virtual async Task<IActionResult> CreateAsync([FromBody] ComponentRequestDto input)
	{
		try
		{
			var createDto = EarningComponentOpenApiMapper.ToCreateDto(input);
			var createdComponent = await _earningComponentAppService.CreateAsync(createDto);
			
			return StatusCode(201, ApiSuccessWithIdResponseDto.SuccessResultWithId(
				"Component created successfully", 
				createdComponent.Id));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error creating earning component");
			throw;
		}
	}

	/// <summary>
	/// Update an existing earning component (OpenAPI contract: accepts ComponentRequest, returns BasicSuccess)
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> UpdateAsync(Guid id, [FromBody] ComponentRequestDto input)
	{
		try
		{
			// Fetch existing component to preserve display order (not in OpenAPI contract)
			var existingComponent = await _earningComponentAppService.GetAsync(id);
			var updateDto = EarningComponentOpenApiMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
			await _earningComponentAppService.UpdateAsync(id, updateDto);
			
			return ApiSuccessResponseDto.SuccessResult("Component updated successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error updating earning component with ID: {Id}", id);
			throw;
		}
	}

	/// <summary>
	/// Delete an earning component (OpenAPI contract: returns BasicSuccess)
	/// </summary>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Delete)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> DeleteAsync(Guid id)
	{
		try
		{
			await _earningComponentAppService.DeleteAsync(id);
			return ApiSuccessResponseDto.SuccessResult("Component deleted successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error deleting earning component with ID: {Id}", id);
			throw;
		}
	}

	#endregion

	#region Additional Endpoints (Not in OpenAPI Contract)

	/// <summary>
	/// Activate an earning component
	/// </summary>
	[HttpPost("{id}/activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> ActivateAsync(Guid id)
	{
		try
		{
			await _earningComponentAppService.ActivateAsync(id);
			return ApiSuccessResponseDto.SuccessResult("Component activated successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error activating earning component with ID: {Id}", id);
			throw;
		}
	}

	/// <summary>
	/// Deactivate an earning component
	/// </summary>
	[HttpPost("{id}/deactivate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	public virtual async Task<ApiSuccessResponseDto> DeactivateAsync(Guid id)
	{
		try
		{
			await _earningComponentAppService.DeactivateAsync(id);
			return ApiSuccessResponseDto.SuccessResult("Component deactivated successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error deactivating earning component with ID: {Id}", id);
			throw;
		}
	}

	#endregion
}
