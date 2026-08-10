using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmEntranceExamReadService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmEntranceExamDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmEntranceExamListInput input);
	Task<AdmissionCrmEntranceExamDto> GetByLeadAsync(Guid leadId);
	Task<AdmissionCrmEligibilityHandoffDto> GetEligibilityHandoffAsync(Guid examRecordId);
}

public interface IAdmissionCrmEntranceExamSchedulingService : IApplicationService
{
	Task<AdmissionCrmEntranceExamDto> ScheduleAsync(Guid leadId, ScheduleAdmissionCrmEntranceExamDto input);
}

public interface IAdmissionCrmEntranceExamAttendanceService : IApplicationService
{
	Task<AdmissionCrmEntranceExamDto> MarkAttendanceAsync(
		Guid examRecordId,
		MarkAdmissionCrmExamAttendanceDto input);
}

public interface IAdmissionCrmEntranceExamResultService : IApplicationService
{
	Task<AdmissionCrmEntranceExamDto> UploadResultAsync(
		Guid examRecordId,
		UploadAdmissionCrmEntranceExamResultDto input);
}

public interface IAdmissionCrmEntranceExamEligibilityService : IApplicationService
{
	Task<AdmissionCrmEntranceExamDto> DecideEligibilityAsync(
		Guid examRecordId,
		UpdateAdmissionCrmEligibilityDecisionDto input);
}
