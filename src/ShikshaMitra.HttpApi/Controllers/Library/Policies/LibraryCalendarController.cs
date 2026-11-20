using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library.Policies;

[Area("Library")]
[Route("api/library/calendar")]
public class LibraryCalendarController(ILibraryCalendarAppService service) : AbpController
{
	[HttpGet("settings")]
	public Task<LibraryCalendarSettingsDto> GetSettingsAsync([FromQuery] Guid? academicYearId = null) => service.GetSettingsAsync(academicYearId);
	[HttpPut("settings")]
	public Task<LibraryCalendarSettingsDto> UpdateSettingsAsync([FromBody] LibraryCalendarSettingsRequest input) => service.UpdateSettingsAsync(input);
	[HttpGet("closures")]
	public Task<PaginatedResponse<LibraryCalendarClosureDto>> GetClosuresAsync([FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) => service.GetClosuresAsync(academicYearId, page, pageSize);
	[HttpPost("closures")]
	public async Task<IActionResult> CreateClosureAsync([FromQuery] Guid academicYearId, [FromBody] LibraryCalendarClosureRequest input) =>
		StatusCode(201, await service.CreateClosureAsync(academicYearId, input));
	[HttpPut("closures/{closureId:guid}")]
	public Task<LibraryCalendarClosureDto> UpdateClosureAsync(Guid closureId, [FromBody] LibraryCalendarClosureRequest input) => service.UpdateClosureAsync(closureId, input);
	[HttpDelete("closures/{closureId:guid}")]
	public Task DeleteClosureAsync(Guid closureId) => service.DeleteClosureAsync(closureId);
	[HttpPost("calculate")]
	public Task<LibraryCalendarCalculationDto> CalculateAsync([FromBody] LibraryCalendarCalculateRequest input) => service.CalculateAsync(input);
}
