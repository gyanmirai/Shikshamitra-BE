using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/day-calculation")]
[Authorize]
public class DayCalculationController : AbpControllerBase
{
	private readonly IDayCalculationSettingAppService _dayCalculationSettingAppService;

	public DayCalculationController(IDayCalculationSettingAppService dayCalculationSettingAppService)
	{
		_dayCalculationSettingAppService = dayCalculationSettingAppService;
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsDayCalculation.Add)]
	[ProducesResponseType(typeof(DayCalculationResponseDto), 200)]
	[ProducesResponseType(400)]
	public virtual async Task<DayCalculationResponseDto> SaveAsync(
		[FromBody] DayCalculationRequestDto input)
	{
		return await _dayCalculationSettingAppService.SaveAsync(input);
	}
}
