using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Enrollments.Inquiries;
public interface IStudentInquiryAppService
{
	Task CreateStudentInquiryAsync(CreateUpdateInquiryDto createStudentInquiryDto);
	Task<StudentDto> GetInquiryByIdAsync(Guid studentId);
	Task<PagedResultDto<StudentDto>> GetInquiriesByMediumAsync(Guid academicMediumId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null);
	Task<StudentDto> UpdateInquiryAsync(Guid studentId, CreateUpdateInquiryDto updateInquiryDto);
	Task AssignRegistrationFormAsync(Guid studentId, IssueRegistrationFormDto issueFormDto);
}
