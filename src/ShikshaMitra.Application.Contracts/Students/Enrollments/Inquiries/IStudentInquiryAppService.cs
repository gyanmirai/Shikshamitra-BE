using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Enrollments.Inquiries;
public interface IStudentInquiryAppService
{
	Task<StudentEnrollmentListDto> CreateStudentInquiryAsync(CreateUpdateInquiryDto createStudentInquiryDto);
	Task<StudentEnrollmentListDto> GetInquiryByIdAsync(Guid studentId);
	Task<PagedResultDto<StudentEnrollmentListDto>> GetInquiriesByMediumAsync(Guid academicMediumId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null);
	Task<StudentEnrollmentListDto> UpdateInquiryAsync(Guid studentId, CreateUpdateInquiryDto updateInquiryDto);
	Task AssignRegistrationFormAsync(Guid studentId, IssueRegistrationFormDto issueFormDto);
}
