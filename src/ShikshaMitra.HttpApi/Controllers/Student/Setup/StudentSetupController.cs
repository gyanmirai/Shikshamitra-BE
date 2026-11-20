using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.Setup;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Student.Setup;

[Authorize, ApiController, Route("api/student/setup")]
public class StudentSetupController(IStudentSetupAppService service) : AbpController
{
	[HttpGet("visit-status")]
	public Task<StudentSetupVisitStatusDto> GetVisitStatusAsync() => service.GetVisitStatusAsync();
	[HttpPost("visit")]
	public Task<StudentSetupVisitDto> RecordVisitAsync([FromBody] StudentSetupVisitInput input) => service.RecordVisitAsync(input);
	[HttpGet("{academicYearId:guid}")]
	public Task<StudentSetupStateDto> GetAsync(Guid academicYearId, [FromQuery] StudentSetupContextInput input) => service.GetAsync(academicYearId, input);
	[HttpPut("{academicYearId:guid}/progress")]
	public Task<StudentSetupStateDto> SaveProgressAsync(Guid academicYearId, [FromBody] StudentSetupProgressInput input) => service.SaveProgressAsync(academicYearId, input);
}
