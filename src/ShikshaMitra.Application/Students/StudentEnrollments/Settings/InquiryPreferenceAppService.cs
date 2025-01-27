using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class InquiryPreferenceAppService(IInquiryPreferenceRepository InquiryPreferenceRepository) : CrudAppService<InquiryPreference, InquiryPreferenceDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateInquiryPreferenceDto>(InquiryPreferenceRepository), IInquiryPreferenceAppService
{
}