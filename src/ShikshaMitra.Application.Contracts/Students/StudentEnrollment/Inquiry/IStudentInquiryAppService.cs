using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollment.Inquiry;

public interface IStudentInquiryAppService : ICrudAppService<StudentInquiryDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentInquiryDto>
{
	
}