using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Setup;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Setup;

[Authorize, ApiController, Route("api/academics/setup")]
public class AcademicSetupController(IAcademicSetupAppService service) : AbpController
{
	[HttpGet("visit-status")]
	public Task<AcademicSetupVisitStatusDto> GetVisitStatusAsync() => service.GetVisitStatusAsync();
	[HttpPost("visit")]
	public Task<AcademicSetupVisitDto> RecordVisitAsync([FromBody] AcademicSetupVisitInput input) => service.RecordVisitAsync(input);
	[HttpGet("{academicYearId:guid}")]
	public Task<AcademicSetupStateDto> GetAsync(Guid academicYearId, [FromQuery] AcademicSetupContextInput input) => service.GetAsync(academicYearId, input);
	[HttpPut("{academicYearId:guid}/progress")]
	public Task<AcademicSetupStateDto> SaveProgressAsync(Guid academicYearId, [FromBody] AcademicSetupProgressInput input) => service.SaveProgressAsync(academicYearId, input);
}
