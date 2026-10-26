using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.ReportingManager;
using ShikshaMitra.HR.Employees.ReportingManagerInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee reporting manager management controller
/// Handles operations for managing employee reporting hierarchy in the school ERP system
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class ReportingManagerController : AbpControllerBase
{
	private readonly IReportingManagerAppService _reportingManagerAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public ReportingManagerController(
		IReportingManagerAppService reportingManagerAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_reportingManagerAppService = reportingManagerAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// Get employee reporting manager
	/// </summary>
	[HttpGet("{employeeId}/reporting-manager")]
	[ProducesResponseType(typeof(ReportingManagerResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<ReportingManagerResponse> GetAsync([FromRoute, Required] Guid employeeId)
	{
		await _authorizationService.CheckEmployeeAccessAsync(employeeId);
		var result = await _reportingManagerAppService.GetReportingManagerAsync(employeeId);
		return ReportingManagerContractMapper.ToResponse(result);
	}

	[HttpGet("reporting-manager/options")]
	public virtual async Task<System.Collections.Generic.List<EmployeeBasicDto>> GetOptionsAsync([FromQuery] string? search = null)
	{
		return await _reportingManagerAppService.GetManagerOptionsAsync(search);
	}

	/// <summary>
	/// Update employee reporting manager
	/// </summary>
	[HttpPut("{employeeId}/reporting-manager")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody] ReportingManagerRequest? request)
	{
		if (request?.ReportingManagerId == null ||
		    request.ReportingManagerKind is not ("Employee" or "TenantAdministrator") ||
		    !Guid.TryParse(request.ReportingManagerId, out var reportingManagerId) ||
		    reportingManagerId == Guid.Empty)
		{
			return BadRequest(new { message = "Reporting manager is required." });
		}

		await _authorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		var assignDto = ReportingManagerContractMapper.ToAssignDto(request);
		await _reportingManagerAppService.AssignReportingManagerAsync(employeeId, assignDto);

		return Ok();
	}

	/// <summary>
	/// Get employees under reporting manager
	/// </summary>
	[HttpGet("reporting-manager/{managerId}/employees")]
	[ProducesResponseType(typeof(EmployeesUnderManagerResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<EmployeesUnderManagerResponse> GetEmployeesUnderManagerAsync(
		[FromRoute, Required] Guid managerId)
	{
		var result = await _reportingManagerAppService.GetEmployeesByManagerAsync(managerId);
		return ReportingManagerContractMapper.ToEmployeesUnderManagerResponse(result);
	}

	/// <summary>
	/// Get employees under the signed-in manager (full hierarchy).
	/// </summary>
	[HttpGet("reporting-manager/my/employees")]
	[ProducesResponseType(typeof(EmployeesUnderManagerResponse), StatusCodes.Status200OK)]
	public virtual async Task<EmployeesUnderManagerResponse> GetMyEmployeesUnderManagerAsync()
	{
		var result = await _reportingManagerAppService.GetMyEmployeesByManagerAsync();
		return ReportingManagerContractMapper.ToEmployeesUnderManagerResponse(result);
	}
}
