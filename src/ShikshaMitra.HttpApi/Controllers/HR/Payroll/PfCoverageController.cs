using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule;

namespace ShikshaMitra.Controllers.HR.Payroll;

[RemoteService(Name = "Default"), Area("app"), Authorize]
[Route("api/payroll/pf-rules/coverage")]
public class PfCoverageController(IPfCoverageAppService service) : AbpControllerBase
{
	[HttpGet, Authorize(Permissions.View)]
	public Task<PfCoverageSettingsDto> GetAsync() => service.GetAsync();
	[HttpPut, Authorize(Permissions.Add)]
	public Task<PfCoverageSettingsDto> SaveAsync([FromBody] SavePfCoverageDto input) => service.SaveAsync(input);
}
