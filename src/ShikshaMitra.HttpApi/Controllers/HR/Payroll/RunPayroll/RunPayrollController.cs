using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.RunPayroll;

/// <summary>
/// API Controller for running payroll (OpenAPI contract compliant)
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/run")]
[Authorize]
public class RunPayrollController : AbpControllerBase
{
	private readonly IRunPayrollAppService _runPayrollAppService;

	public RunPayrollController(IRunPayrollAppService runPayrollAppService)
	{
		_runPayrollAppService = runPayrollAppService;
	}

	/// <summary>
	/// Get payroll preview for a specific month (paginated)
	/// Returns employee payroll breakdown before locking
	/// </summary>
	/// <param name="month">Payroll month (supports YYYY-MM, MMMM-YYYY, and common date-like formats)</param>
	/// <param name="page">Page index (0-based), defaults to 0</param>
	/// <param name="pageSize">Number of items per page (10-100), defaults to 50</param>
	/// <returns>Payroll preview with employee payroll items</returns>
	/// <response code="200">Payroll preview retrieved successfully</response>
	/// <response code="400">Invalid month format or other validation errors</response>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
	[ProducesResponseType(typeof(RunPayrollResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<RunPayrollResponse> GetPreviewAsync(
		[FromQuery, Required] string month,
		[FromQuery, Range(0, int.MaxValue)] int page = 0,
		[FromQuery, Range(10, 100)] int pageSize = 50,
		[FromQuery] bool forceRefresh = false)
	{
		return await _runPayrollAppService.GetPreviewAsync(new GetRunPayrollInput
		{
			Month = month,
			Page = page,
			PageSize = pageSize,
			ForceRefresh = forceRefresh
		});
	}

	/// <summary>
	/// Generate and lock payroll for a specific month
	/// Creates finalized salary slips for all active employees
	/// </summary>
	/// <param name="request">Request containing month to lock</param>
	/// <returns>Lock confirmation with success message</returns>
	/// <response code="200">Payroll locked successfully</response>
	/// <response code="400">Payroll already locked or validation errors</response>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add)]
	[ProducesResponseType(typeof(PayrollLockResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<PayrollLockResponse> LockPayrollAsync(
		[FromBody, Required] RunPayrollRequest request)
	{
		return await _runPayrollAppService.LockPayrollAsync(request);
	}

	/// <summary>
	/// Marks selected payroll rows as reviewed before lock.
	/// </summary>
	[HttpPost("reviews")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add)]
	[ProducesResponseType(typeof(PayrollLockResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<PayrollLockResponse> MarkReviewedAsync(
		[FromBody, Required] PayrollRunReviewRequest request)
	{
		return await _runPayrollAppService.MarkReviewedAsync(request);
	}

	/// <summary>
	/// Removes review markers from selected payroll rows before lock.
	/// </summary>
	[HttpDelete("reviews")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add)]
	[ProducesResponseType(typeof(PayrollLockResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<PayrollLockResponse> UnmarkReviewedAsync(
		[FromBody, Required] PayrollRunReviewRequest request)
	{
		return await _runPayrollAppService.UnmarkReviewedAsync(request);
	}
}

