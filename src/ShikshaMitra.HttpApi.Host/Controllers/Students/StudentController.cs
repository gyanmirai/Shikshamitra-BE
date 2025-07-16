using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students;

[ApiController]
[Authorize]
[Route("api/students")]
public class StudentController(IStudentInquiryAppService studentInquiryAppService) : AbpControllerBase
{
	[HttpPost("inquiries")]
	public async Task<Result> CreateInquiry(
		[FromBody] CreateUpdateInquiryDto createInquiryDto)
	{
		var createdInquiry = await studentInquiryAppService.CreateStudentInquiryAsync(createInquiryDto);
		return Result.Success(createdInquiry);
	}

	//[HttpGet("inquiries/{academicMediumId}")]
	//public Task<PagedResultDto<StudentInquiryDto>> GetInquiriesByMedium(
	//	Guid academicMediumId,
	//	int skipCount = 0,
	//	int maxResultCount = 10)
	//{
	//}

	//[HttpGet("{studentId}/inquiries")]
	//public Task<StudentInquiryDto> UpdateInquiry(
	//	Guid studentId,
	//	[FromBody] CreateUpdateInquiryDto request)
	//{
	//}

	//[HttpGet("{studentId}/inquiries/registration-forms")]
	//public Task AssignRegistrationForm(
	//	Guid studentId,
	//	[FromBody] CreateUpdateInquiryDto request)
	//{
	//}
}
