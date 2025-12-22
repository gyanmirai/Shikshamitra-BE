using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students")]
public class StudentListController(
	IStudentAppService studentAppService
) : AbpController
{
	[HttpGet]
	public async Task<Result<PagedResultDto<StudentDto>>> GetStudentsByDivisionAsync(
		[FromQuery] Guid divisionId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		var studentInquiries = await studentAppService.GetStudentsByDivisionAsync(
			divisionId,
			skipCount,
			maxResultCount,
			searchTerm
		);
		return Result.Success(studentInquiries);
	}
}
