using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students")]
public class StudentListController : AbpController
{
	[HttpGet("{academicMediumId}/inquiries")]
	public async Task<Result<PagedResultDto<StudentEnrollmentListDto>>> GetInquiriesByMedium(
		Guid academicMediumId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		var studentInquiries = await studentInquiryAppService.GetInquiriesByMediumAsync(
			academicMediumId,
			skipCount,
			maxResultCount,
			searchTerm
		);
		return Result.Success(studentInquiries);
	}
}
