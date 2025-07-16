using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students;

[ApiController]
[Authorize]
[Route("api/students")]
public class StudentController(IStudentInquiryAppService studentInquiryAppService) : AbpControllerBase
{
	[HttpPost("inquiries")]
	public async Task<StudentInquiryDto> CreateInquiry(
		[FromBody] CreateUpdateInquiryDto createInquiryDto)
	{
		return await studentInquiryAppService.CreateStudentInquiryAsync(createInquiryDto);
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
