using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.SalaryStructure;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/education-medium-stage/{educationMediumStageId}/payroll-structure")]
[Authorize]
public class PayrollStructureLegacyCompatibilityController : AbpControllerBase
{
	[HttpGet]
	public IActionResult GetLegacyPayrollStructure(Guid educationMediumStageId)
	{
		return BadRequest(new
		{
			type = "https://httpstatuses.com/400",
			title = "Business rule violated",
			status = 400,
			detail = "This payroll structure endpoint is deprecated. Use /api/payroll/employees/{employeeId}/salary-structure.",
			instance = $"/api/education-medium-stage/{educationMediumStageId}/payroll-structure",
			errorCode = "HR:Payroll:030020",
			data = new
			{
				educationMediumStageId,
				useEndpoint = "/api/payroll/employees/{employeeId}/salary-structure"
			}
		});
	}
}
