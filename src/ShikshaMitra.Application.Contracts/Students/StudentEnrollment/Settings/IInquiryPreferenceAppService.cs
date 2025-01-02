using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public interface IInquiryPreferenceAppService : ICrudAppService<InquiryPreferenceDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateInquiryPreferenceDto>
{
	
}