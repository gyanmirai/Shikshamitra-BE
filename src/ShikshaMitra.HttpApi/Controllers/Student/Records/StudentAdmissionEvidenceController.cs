using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Route("api/student/records/students/{studentId:guid}/admission-evidence")]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments.View)]
public class StudentAdmissionEvidenceController(IStudentAdmissionEvidenceAppService service) : AbpController
{
	[HttpGet]
	public async Task<Result<List<StudentAdmissionEvidenceDto>>> GetListAsync(Guid studentId) =>
		Result.Success(await service.GetListAsync(studentId));

	[HttpGet("{evidenceId:guid}/download")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments.Download)]
	public async Task<IActionResult> DownloadAsync(Guid studentId, Guid evidenceId)
	{
		var file = await service.DownloadAsync(studentId, evidenceId);
		Response.Headers["Cache-Control"] = "private, no-store";
		Response.Headers["X-Content-Type-Options"] = "nosniff";
		return File(file.Content, file.ContentType, file.FileName);
	}
}
