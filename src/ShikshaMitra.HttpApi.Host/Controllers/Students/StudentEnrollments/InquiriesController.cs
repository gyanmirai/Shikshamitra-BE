using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

[ApiController]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Default)]
[Route("api/inquiries")]
public class InquiriesController(IStudentInquiryAppService studentInquiryAppService) : AbpControllerBase
{
    [HttpPost]
    public async Task<Result> CreateInquiry([FromBody] CreateUpdateInquiryDto createInquiryDto)
    {
        var createdInquiry = await studentInquiryAppService.CreateStudentInquiryAsync(createInquiryDto);
        return Result.Success(createdInquiry);
    }

    [HttpGet("{academicMediumId}")]
    public async Task<Result<PagedResultDto<StudentInquiryDto>>> GetInquiriesByMedium(
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

    [HttpPut("{studentId}")]
    public async Task<Result<StudentInquiryDto>> UpdateInquiry(
        Guid studentId,
        [FromBody] CreateUpdateInquiryDto updateInquiryDto)
    {
        var updatedInquiry = await studentInquiryAppService.UpdateInquiryAsync(studentId, updateInquiryDto);
        return Result.Success(updatedInquiry);
    }
}
