using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

	public ReportingManagerController(IReportingManagerAppService reportingManagerAppService)
	{
		_reportingManagerAppService = reportingManagerAppService;
	}

	/// <summary>
	/// Get employee reporting manager
	/// </summary>
	[HttpGet("{employeeId}/reporting-manager")]
	[ProducesResponseType(typeof(ReportingManagerResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<ReportingManagerResponse> GetAsync([FromRoute, Required] Guid employeeId)
	{
		var result = await _reportingManagerAppService.GetReportingManagerAsync(employeeId);
		return ReportingManagerContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Update employee reporting manager
	/// </summary>
	[HttpPut("{employeeId}/reporting-manager")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] ReportingManagerRequest request)
	{
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
}
