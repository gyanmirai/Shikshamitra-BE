using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.SalarySlips;

[Area("app"), RemoteService(Name = "Default"), Authorize, Route("api/payroll/payslip-corrections")]
public class PayslipCorrectionController(IPayslipCorrectionAppService corrections) : AbpControllerBase
{
	[HttpGet] public Task<List<PayslipCorrectionDto>> GetAsync(Guid documentId) => corrections.GetAsync(documentId);
	[HttpPost] public Task<PayslipCorrectionDto> StartAsync(StartPayslipCorrectionDto input) => corrections.StartAsync(input);
	[HttpPost("{id}/calculate")] public Task<PayslipCorrectionDto> CalculateAsync(Guid id, CalculatePayslipCorrectionDto input) => corrections.CalculateAsync(id, input);
	[HttpPost("{id}/review")] public Task<PayslipCorrectionDto> ReviewAsync(Guid id, PayslipCorrectionActionDto input) => corrections.ReviewAsync(id, input);
	[HttpPost("{id}/cancel")] public Task<PayslipCorrectionDto> CancelAsync(Guid id, PayslipCorrectionActionDto input) => corrections.CancelAsync(id, input);
	[HttpPost("{id}/apply")] public Task<PayslipCorrectionDto> ApplyAsync(Guid id, PayslipCorrectionActionDto input) => corrections.ApplyAsync(id, input);
	[HttpPost("{id}/settle")] public Task<PayslipCorrectionDto> SettleAsync(Guid id, SettlePayslipCorrectionDto input) => corrections.SettleAsync(id, input);
}
