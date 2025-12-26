using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.Records;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records")]
public class StudentManagementController(
	IStudentManagementAppService studentManagementAppService
) : AbpController
{
	[HttpGet("{divisionId}")]
	public async Task<Result<List<StudentRecordDto>>> GetStudentRecords(Guid divisionId)
	{
		var studentRecords = await studentManagementAppService.GetStudentRecords(divisionId);
		return Result.Success(studentRecords);
	}
}
