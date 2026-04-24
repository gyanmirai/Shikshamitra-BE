using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/attendance/integration-runs")]
[Authorize]
public class AttendanceIntegrationRunController(IAttendanceIntegrationRunAppService integrationRunAppService)
	: AbpControllerBase
{
	[HttpPost]
	[ProducesResponseType(typeof(AttendanceIntegrationRunResponseDto), StatusCodes.Status200OK)]
	public Task<AttendanceIntegrationRunResponseDto> RecordAsync([FromBody, Required] AttendanceIntegrationRunRequestDto input)
	{
		return integrationRunAppService.RecordIntegrationRunAsync(input);
	}
}