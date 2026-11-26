using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.RunPayroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/run/previews")]
[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View)]
public class PayrollPreviewRunController : AbpControllerBase
{
	private readonly IPayrollPreviewRunAppService _service;

	public PayrollPreviewRunController(IPayrollPreviewRunAppService service)
	{
		_service = service;
		PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ControllerActivated);
	}

	[HttpPost]
	public async Task<IActionResult> StartAsync([FromBody] StartPayrollPreviewRequest request)
	{
		var run = await _service.StartAsync(request);
		return Accepted($"/api/payroll/run/previews/{run.Id}", run);
	}
	[HttpGet("current")]
	public async Task<PayrollPreviewLookupDto> GetCurrentAsync([FromQuery] PayrollPreviewMonthInput input)
	{
		PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ActionStarted);
		try { return await _service.GetCurrentAsync(input); }
		finally { PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ActionCompleted); }
	}

	[HttpGet("/api/payroll/run/register")]
	public async Task<RunPayrollResponse> GetRegisterAsync([FromQuery] PayrollRegisterInput input)
	{
		PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ActionStarted);
		try { return await _service.GetRegisterAsync(input); }
		finally { PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ActionCompleted); }
	}

	[HttpGet("{id:guid}")]
	public Task<PayrollPreviewStatusDto> GetAsync(Guid id) => _service.GetAsync(id);
	[HttpGet("{id:guid}/employees")]
	public Task<RunPayrollResponse> GetEmployeesAsync(Guid id, [FromQuery] PayrollPreviewPageInput input) => _service.GetEmployeesAsync(id, input);
}
