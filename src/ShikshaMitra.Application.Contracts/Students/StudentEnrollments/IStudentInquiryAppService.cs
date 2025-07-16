using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments;
public interface IStudentInquiryAppService
{
	Task<StudentInquiryDto> CreateStudentInquiryAsync(CreateUpdateInquiryDto createStudentInquiryDto);
	Task<PagedResultDto<StudentInquiryDto>> GetInquiriesByMediumAsync(Guid academicMediumId, int skipCount = 0, int maxResultCount = 10);
	Task<StudentInquiryDto> UpdateInquiryAsync(Guid studentId, CreateUpdateInquiryDto updateInquiryDto);
	Task AssignRegistrationFormAsync(Guid studentId, CreateUpdateInquiryDto createUpdateInquiryDto);
}
