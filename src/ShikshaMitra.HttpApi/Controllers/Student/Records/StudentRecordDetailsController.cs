using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Route("api/students/records/{studentId:guid}/details")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
public class StudentRecordDetailsController(IStudentRecordDetailsAppService service, IStudentRecordFormAppService forms) : AbpController
{
	[HttpGet]
	public async Task<Result<StudentRecordDetailsDto>> GetAsync(Guid studentId, [FromQuery] Guid? academicYearId = null) =>
		Result.Success(await service.GetAsync(studentId, academicYearId));
	[HttpGet("options")]
	public async Task<Result<List<AdmissionFormOptionDto>>> GetFieldOptionsAsync(Guid studentId, [FromQuery] StudentRecordFieldOptionsInput input) =>
		Result.Success(await service.GetFieldOptionsAsync(studentId, input));
	[HttpGet("form-snapshot"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
	public Task<StudentRecordSectionSnapshotDto> GetFormSnapshotAsync(Guid studentId) => forms.GetSnapshotAsync(studentId);
	[HttpPut("form"), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View), Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
	public Task<StudentRecordChangeDto> SaveFormAsync(Guid studentId, [FromBody] SaveStudentRecordFormDto input) => forms.SaveAsync(studentId, input);
}
