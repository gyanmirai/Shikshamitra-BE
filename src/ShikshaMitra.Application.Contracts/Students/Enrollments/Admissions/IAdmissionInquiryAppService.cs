using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionInquiryAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionInquiryDto>> GetListAsync(AdmissionInquiryListInput input);
	Task<AdmissionInquiryDto> GetAsync(Guid id);
	Task<AdmissionInquiryDto> CreateAsync(CreateAdmissionInquiryDto input);
	Task<AdmissionInquiryDto> SetStatusAsync(Guid id, AdmissionInquiryStatus status, Guid? duplicateOfId = null);
	Task<AdmissionInquiryDto> MarkContactedAsync(Guid id);
	Task<System.Collections.Generic.List<AdmissionInquiryDuplicateMatchDto>> DuplicateCheckAsync(AdmissionInquiryDuplicateCheckInput input);
	Task<AdmissionInquiryDto> AssignCounsellorAsync(Guid id, AssignAdmissionCounsellorDto input);
}
