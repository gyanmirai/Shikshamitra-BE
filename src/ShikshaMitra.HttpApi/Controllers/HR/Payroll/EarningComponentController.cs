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

namespace ShikshaMitra.Controllers.HR.Payroll;

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
	/// Get paginated list of earning components (OpenAPI contract)
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.View)]
	[ProducesResponseType(typeof(DeductionComponentPaginatedResponseDto), 200)]
	public virtual async Task<DeductionComponentPaginatedResponseDto> GetPaginatedAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
	{
		var result = await _earningComponentAppService.GetListAsync(new PagedAndSortedResultRequestDto
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize
		});

			return EarningComponentMapper.ToPaginatedResponse(result, page, pageSize);
		}

	/// <summary>
	/// Create a new earning component (OpenAPI contract)
	/// </summary>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(typeof(ComponentCreateResponseDto), 201)]
	public virtual async Task<ActionResult<ComponentCreateResponseDto>> CreateAsync([FromBody] PayrollComponentCreateRequestDto input)
	{
		var createDto = EarningComponentMapper.ToCreateDto(input);
		var createdComponent = await _earningComponentAppService.CreateAsync(createDto);
		return StatusCode(201, new ComponentCreateResponseDto
		{
			Id = createdComponent.Id,
			CreatedAt = DateTime.UtcNow
		});
	}

	/// <summary>
	/// Update an existing earning component (OpenAPI contract)
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Add)]
	[ProducesResponseType(200)]
	public virtual async Task<IActionResult> UpdateAsync(Guid id, [FromBody] PayrollComponentCreateRequestDto input)
	{
		var existingComponent = await _earningComponentAppService.GetAsync(id);
		var updateDto = EarningComponentMapper.ToUpdateDto(input, existingComponent.DisplayOrder);
		await _earningComponentAppService.UpdateAsync(id, updateDto);
		return Ok();
	}

	/// <summary>
	/// Delete an earning component (OpenAPI contract)
	/// </summary>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsEarningComponents.Delete)]
	[ProducesResponseType(204)]
	public virtual async Task<IActionResult> DeleteAsync(Guid id)
	{
		await _earningComponentAppService.DeleteAsync(id);
		return NoContent();
	}

		#endregion
	}
