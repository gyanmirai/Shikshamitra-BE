using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmEntranceExamAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmEntranceExamDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmEntranceExamListInput input);
	Task<AdmissionCrmEntranceExamDto> GetByLeadAsync(Guid leadId);
	Task<AdmissionCrmEligibilityHandoffDto> GetEligibilityHandoffAsync(Guid examRecordId);
	Task<AdmissionCrmEntranceExamDto> ScheduleAsync(Guid leadId, ScheduleAdmissionCrmEntranceExamDto input);
	Task<AdmissionCrmEntranceExamDto> ScheduleFromApplicantAsync(Guid leadId, Guid entranceExamSlotId);
	Task<AdmissionCrmEntranceExamDto> MarkAttendanceAsync(Guid examRecordId, MarkAdmissionCrmExamAttendanceDto input);
	Task<AdmissionCrmEntranceExamDto> UploadResultAsync(Guid examRecordId, UploadAdmissionCrmEntranceExamResultDto input);
	Task<AdmissionCrmEntranceExamDto> DecideEligibilityAsync(
		Guid examRecordId,
		UpdateAdmissionCrmEligibilityDecisionDto input);
}
