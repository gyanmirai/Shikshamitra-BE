using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.SalarySlips;

[Area("app"), RemoteService(Name = "Default"), Authorize, Route("api/payroll/payslip-documents")]
public class PayslipDocumentController(IPayslipDocumentAppService documents) : AbpControllerBase
{
	[HttpGet] public Task<List<PayslipDocumentDto>> GetAsync(Guid employeeId, string monthYear) => documents.GetAsync(employeeId, monthYear);
	[HttpGet("{id}/pdf")]
	public async Task<IActionResult> GetPdfAsync(Guid id) => File(await documents.GetPdfAsync(id), "application/pdf", $"Payslip_{id:N}.pdf");
	[HttpPost("payment")] public Task RecordPaymentAsync(RecordPayslipPaymentDto input) => documents.RecordPaymentAsync(input);
	[HttpPost("replace-facts")] public Task<PayslipDocumentDto> ReplaceFactsAsync(ReplacePayslipFactsDto input) => documents.ReplaceFactsAsync(input);
}
