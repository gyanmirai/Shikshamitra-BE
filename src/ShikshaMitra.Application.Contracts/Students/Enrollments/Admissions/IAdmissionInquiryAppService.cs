using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionInquiryAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionInquiryDto>> GetListAsync(AdmissionInquiryListInput input);
	Task<byte[]> ExportCsvAsync(AdmissionInquiryListInput input);
	Task<AdmissionInquiryDto> GetAsync(Guid id);
	Task<AdmissionInquiryDto> CreateAsync(CreateAdmissionInquiryDto input);
	Task<AdmissionInquiryDto> UpdateAsync(Guid id, UpdateAdmissionInquiryDto input);
	Task<List<AdmissionInquiryTimelineItemDto>> GetTimelineAsync(Guid id);
	Task<AdmissionInquiryDto> SetStatusAsync(Guid id, AdmissionInquiryStatus status, Guid? duplicateOfId = null);
	Task<AdmissionInquiryDto> MarkContactedAsync(Guid id);
	Task<System.Collections.Generic.List<AdmissionInquiryDuplicateMatchDto>> DuplicateCheckAsync(AdmissionInquiryDuplicateCheckInput input);
	Task<List<AdmissionInquiryCounsellorDto>> GetAssignableCounsellorsAsync(Guid? admissionOfferingId = null, string? filter = null);
	Task<AdmissionInquiryDto> AssignCounsellorAsync(Guid id, AssignAdmissionCounsellorDto input);
	Task<BulkAdmissionInquiryResultDto> BulkAssignCounsellorAsync(BulkAssignAdmissionInquiriesDto input);
	Task<AdmissionInquiryCommunicationResultDto> CreateCommunicationAsync(Guid id, CreateAdmissionInquiryCommunicationDto input);
}
