using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionInquiryManagementAppService : IApplicationService
{
	Task<AdmissionInquiryManagementDto> GetAsync(Guid inquiryId);
	Task<AdmissionInquiryManagementDto> RecordContactAsync(Guid inquiryId, RecordAdmissionInquiryContactDto input);
	Task<AdmissionInquiryManagementDto> RecordFollowUpNoteAsync(Guid inquiryId, RecordAdmissionInquiryFollowUpNoteDto input);
	Task<AdmissionInquiryManagementDto> ScheduleFollowUpAsync(Guid inquiryId, ScheduleAdmissionInquiryFollowUpDto input);
	Task<AdmissionInquiryManagementDto> CompleteFollowUpAsync(Guid inquiryId, Guid followUpId, CompleteAdmissionInquiryFollowUpDto input);
	Task<AdmissionInquiryManagementDto> CancelFollowUpAsync(Guid inquiryId, Guid followUpId, CancelAdmissionInquiryFollowUpDto input);
	Task<AdmissionInquiryManagementDto> QualifyAsync(Guid inquiryId, QualifyAdmissionInquiryDto input);
	Task<AdmissionInquiryManagementDto> ReopenAsync(Guid inquiryId, ReopenAdmissionInquiryDto input);
}
