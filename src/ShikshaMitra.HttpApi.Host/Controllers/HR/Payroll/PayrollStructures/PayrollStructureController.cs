using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.PayrollStructures;

[Route("api/education-medium-stage/{educationMediumStageId}/payroll-structure")]
public class PayrollStructureController(IPayrollStructureAppService payrollStructureAppService) : AbpController
{
	//[HttpGet]
	//[Route("")]
	//public async Task<IActionResult> GetPayrollStructures(Guid educationMediumStageId)
	//{
	//	List<PayrollStructureDto> payrollStructures =
	//		await payrollStructureAppService.GetPayrollStructuresByEducationMediumStageId(educationMediumStageId);

	//	return Ok(payrollStructures);
	//}
}