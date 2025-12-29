using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

/// <summary>
/// API Controller for managing PF (Provident Fund) rules.
/// Implements OpenAPI contract-compliant endpoints.
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/pf-rules")]
[Authorize]
public class PfRuleController : AbpControllerBase
{
	private readonly IProvidentFundAppService _providentFundAppService;

	public PfRuleController(IProvidentFundAppService providentFundAppService)
	{
		_providentFundAppService = providentFundAppService;
	}

	/// <summary>
	/// Create a new PF rule
	/// </summary>
	/// <param name="input">PF rule creation request</param>
	/// <returns>Created PF rule with ID and creation timestamp</returns>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.Add)]
	[ProducesResponseType(typeof(PfRuleCreateResponseDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	public virtual async Task<PfRuleCreateResponseDto> CreateAsync([FromBody] CreateUpdateProvidentFundDto input)
	{
		var createdPfRule = await _providentFundAppService.CreateAsync(input);
		return PfRuleOpenApiMapper.ToCreateResponse(createdPfRule);
	}

	/// <summary>
	/// Get a specific PF rule by ID
	/// </summary>
	/// <param name="id">PF rule ID</param>
	/// <returns>PF rule details</returns>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.View)]
	[ProducesResponseType(typeof(PfRuleResponseDto), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	public virtual async Task<PfRuleResponseDto> GetAsync(Guid id)
	{
		var pfRule = await _providentFundAppService.GetAsync(id);
		return PfRuleOpenApiMapper.ToPfRuleResponse(pfRule);
	}

	/// <summary>
	/// Get paginated list of PF rules
	/// </summary>
	/// <param name="page">Page number (1-based, default: 1)</param>
	/// <param name="pageSize">Number of items per page (default: 20, min: 10, max: 100)</param>
	/// <returns>Paginated list of PF rules</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.View)]
	[ProducesResponseType(typeof(PaginatedPfRuleResponseDto), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	public virtual async Task<PaginatedPfRuleResponseDto> GetListAsync(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20)
	{
		// Validate and constrain pagination parameters
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 10, 100);

		var skipCount = (page - 1) * pageSize;

		var input = new PagedAndSortedResultRequestDto
		{
			SkipCount = skipCount,
			MaxResultCount = pageSize
		};

		var result = await _providentFundAppService.GetListAsync(input);

		return PfRuleOpenApiMapper.ToPaginatedResponse(result, page, pageSize);
	}

		/// <summary>
		/// Update an existing PF rule
		/// </summary>
		/// <param name="id">PF rule ID</param>
		/// <param name="input">PF rule update request</param>
		/// <returns>Updated PF rule with ID and update timestamp</returns>
		[HttpPut("{id}")]
		[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.Add)]
		[ProducesResponseType(typeof(PfRuleUpdateResponseDto), 200)]
		[ProducesResponseType(400)]
		[ProducesResponseType(401)]
		[ProducesResponseType(403)]
		[ProducesResponseType(404)]
		public virtual async Task<PfRuleUpdateResponseDto> UpdateAsync(Guid id, [FromBody] CreateUpdateProvidentFundDto input)
		{
			var updatedPfRule = await _providentFundAppService.UpdateAsync(id, input);
			return PfRuleOpenApiMapper.ToUpdateResponse(updatedPfRule);
		}

		/// <summary>
		/// Delete a PF rule
		/// </summary>
		/// <param name="id">PF rule ID</param>
		/// <returns>No content on success</returns>
		[HttpDelete("{id}")]
		[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule.Delete)]
		[ProducesResponseType(204)]
		[ProducesResponseType(401)]
		[ProducesResponseType(403)]
		[ProducesResponseType(404)]
		public virtual async Task DeleteAsync(Guid id)
		{
			await _providentFundAppService.DeleteAsync(id);
		}
	}

