using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.SalarySlips;

[Area("app"), RemoteService(Name = "Default"), Authorize, Route("api/payroll/payslip-settings")]
public class PayslipSettingsController(IPayslipSettingsAppService settings) : AbpControllerBase
{
	[HttpGet] public Task<PayslipSettingsDto> GetAsync() => settings.GetAsync();
	[HttpPut] public Task<PayslipSettingsDto> SaveAsync(PayslipSettingsDto input) => settings.SaveAsync(input);
}
