using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Inquiry;

public interface IStudentInquiryAppService : ICrudAppService<StudentInquiryDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentInquiryDto>
{
	
}