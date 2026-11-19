using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.SalarySlips;

[Area("app"), RemoteService(Name = "Default"), Authorize, Route("api/payroll/payslip-deliveries")]
public class PayslipDeliveryController(IPayslipDeliveryAppService delivery) : AbpControllerBase
{
	[HttpGet] public Task<List<PayslipDeliveryDto>> GetAsync(Guid salarySlipId) => delivery.GetAsync(salarySlipId);
	[HttpPost] public Task<PayslipDeliveryDto> SendAsync(SendPayslipDto input) => delivery.SendAsync(input);
}
