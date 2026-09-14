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
public class PayrollPreviewRunController(IPayrollPreviewRunAppService service) : AbpControllerBase
{
	[HttpPost]
	public async Task<IActionResult> StartAsync([FromBody] StartPayrollPreviewRequest request)
	{
		var run = await service.StartAsync(request);
		return Accepted($"/api/payroll/run/previews/{run.Id}", run);
	}
	[HttpGet("{id:guid}")]
	public Task<PayrollPreviewStatusDto> GetAsync(Guid id) => service.GetAsync(id);
	[HttpGet("{id:guid}/employees")]
	public Task<RunPayrollResponse> GetEmployeesAsync(Guid id, [FromQuery] PayrollPreviewPageInput input) => service.GetEmployeesAsync(id, input);
}
