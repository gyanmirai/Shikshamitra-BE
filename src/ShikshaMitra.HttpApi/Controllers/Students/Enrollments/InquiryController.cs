using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Inquiries;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

[ApiController]
[Authorize]
[Route("api/student/enrollment")]
public class InquiryController(IStudentInquiryAppService studentInquiryAppService) : AbpControllerBase
{
    [HttpPost("inquiries")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Inquiry.Add)]
	public async Task<Result> CreateInquiry([FromBody] CreateUpdateInquiryDto createInquiryDto)
    {
        var createdInquiry = await studentInquiryAppService.CreateStudentInquiryAsync(createInquiryDto);
        return Result.Success(createdInquiry);
    }

    [HttpGet("inquiries/{studentId}")]
    public async Task<Result<StudentEnrollmentListDto>> GetInquiryById(Guid studentId)
    {
        var inquiry = await studentInquiryAppService.GetInquiryByIdAsync(studentId);
        return Result.Success(inquiry);
    }

	[HttpPut("inquiries/{studentId}")]
	public async Task<Result<StudentEnrollmentListDto>> UpdateInquiry(
		Guid studentId,
		[FromBody] CreateUpdateInquiryDto updateInquiryDto)
	{
		var updatedInquiry = await studentInquiryAppService.UpdateInquiryAsync(studentId, updateInquiryDto);
		return Result.Success(updatedInquiry);
	}

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
