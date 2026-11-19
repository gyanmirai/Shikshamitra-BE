using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.Portal;
using ShikshaMitra.Common.Responses;
using Volo.Abp;

namespace ShikshaMitra.Controllers.Academic.Timetable;

[ApiController]
[Authorize]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/mobile/v1/academics/timetable/elective-preferences")]
public class PortalElectivePreferenceController(IPortalElectivePreferenceAppService service) : ShikshaMitraController
{
	[HttpGet]
	public async Task<Result<PortalElectivePreferencesDto>> GetAsync([FromQuery] Guid studentId, [FromQuery] Guid academicYearId) =>
		Result.Success(await service.GetAsync(studentId, academicYearId));

	[HttpPost]
	[ProducesResponseType(409)]
	public async Task<Result<PortalElectivePreferencesDto>> SubmitAsync([FromBody] PortalElectivePreferenceSubmitInput input) =>
		Result.Success(await service.SubmitAsync(input));
}
