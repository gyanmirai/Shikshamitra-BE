using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// API Controller for managing deduction components
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
	/// Get list of all deduction components (paginated)
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	public virtual Task<PagedResultDto<DeductionComponentDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return _deductionComponentAppService.GetListAsync(input);
	}

	/// <summary>
	/// Get all active deduction components with pagination support
	/// </summary>
	[HttpGet("active")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	public virtual Task<PagedResultDto<DeductionComponentDto>> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)
	{
		return _deductionComponentAppService.GetAllActiveAsync(input);
	}

	/// <summary>
	/// Get a specific deduction component by ID
	/// </summary>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.View)]
	public virtual Task<DeductionComponentDto> GetAsync(Guid id)
	{
		return _deductionComponentAppService.GetAsync(id);
	}

	/// <summary>
	/// Create a new deduction component
	/// </summary>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	public virtual Task<DeductionComponentDto> CreateAsync([FromBody] CreateDeductionComponentDto input)
	{
		return _deductionComponentAppService.CreateAsync(input);
	}

	/// <summary>
	/// Update an existing deduction component
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	public virtual Task<DeductionComponentDto> UpdateAsync(Guid id, [FromBody] UpdateDeductionComponentDto input)
	{
		return _deductionComponentAppService.UpdateAsync(id, input);
	}

	/// <summary>
	/// Delete a deduction component
	/// </summary>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Delete)]
	public virtual Task DeleteAsync(Guid id)
	{
		return _deductionComponentAppService.DeleteAsync(id);
	}

	/// <summary>
	/// Activate a deduction component
	/// </summary>
	[HttpPost("{id}/activate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	public virtual Task ActivateAsync(Guid id)
	{
		return _deductionComponentAppService.ActivateAsync(id);
	}

	/// <summary>
	/// Deactivate a deduction component
	/// </summary>
	[HttpPost("{id}/deactivate")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDeductionComponents.Add)]
	public virtual Task DeactivateAsync(Guid id)
	{
		return _deductionComponentAppService.DeactivateAsync(id);
	}
}
