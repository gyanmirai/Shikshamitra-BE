using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View)]
[Route("api/students/records/duplicates")]
public class StudentRecordDuplicatesController(IStudentRecordDuplicateAppService duplicates) : AbpController
{
	[HttpGet] public Task<PagedResultDto<StudentRecordDuplicateDto>> List([FromQuery] StudentRecordDuplicateQuery input) => duplicates.GetListAsync(input);
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
	public Task<StudentRecordDuplicateDto> Create(StudentRecordDuplicateInput input) => duplicates.CreateAsync(input);
	[HttpPost("{id}/resolve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve)]
	public Task<StudentRecordDuplicateDto> Resolve(Guid id, StudentRecordDuplicateDecision input) => duplicates.ResolveAsync(id, input);
}
