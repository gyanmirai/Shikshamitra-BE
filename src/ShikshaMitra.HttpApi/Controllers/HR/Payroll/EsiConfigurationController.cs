using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using EsiPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes;

namespace ShikshaMitra.Controllers.HR.Payroll;

[RemoteService(Name = "Default")]
[Authorize]
[Area("app")]
[Route("api/payroll/statutory-schemes/esi/configuration")]
public class EsiConfigurationController(IEsiConfigurationAppService service) : AbpControllerBase
{
	[HttpGet, Authorize(EsiPermissions.View)]
	public Task<EsiConfigurationSettingsDto> GetAsync() => service.GetAsync();
	[HttpPut, Authorize(EsiPermissions.Edit), Authorize(EsiPermissions.Activate)]
	public Task<EsiConfigurationSettingsDto> SaveAsync([FromBody] SaveEsiConfigurationDto input) => service.SaveAsync(input);
}
