using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionAssessmentAppService : IApplicationService
{
	Task<AdmissionAssessmentSessionResourcesDto> GetSessionResourcesAsync();
	Task<List<AdmissionAssessmentStaffDto>> GetInterviewersAsync(string? search);
	Task<PagedResultDto<AdmissionAssessmentSessionDto>> GetSessionWorkbenchAsync(AdmissionAssessmentSessionListInput input);
	Task<AdmissionAssessmentSessionDto> GetSessionAsync(Guid id);
	Task<List<AdmissionAssessmentInterviewSlotDto>> GetSessionSlotsAsync(Guid id);
	Task<AdmissionAssessmentSessionPreviewDto> PreviewSessionAsync(AdmissionAssessmentSessionPreviewInput input);
	Task<AdmissionAssessmentSessionConfirmationDto> ConfirmSessionAsync(AdmissionAssessmentSessionConfirmInput input);
	Task<List<AdmissionAssessmentScopeDto>> GetScopesAsync();
	Task<AdmissionAssessmentRosterDto> GetApplicantsAsync(AdmissionAssessmentRosterInput input);
	Task<List<AdmissionAssessmentSessionDto>> GetSessionsAsync(Guid admissionOfferingId, Guid workflowStageDefinitionId);
	Task<AdmissionAssessmentSessionDto> CreateSessionAsync(AdmissionAssessmentSessionInput input);
	Task<AdmissionAssessmentSessionDto> UpdateSessionAsync(Guid id, AdmissionAssessmentSessionInput input);
	Task<AdmissionAssessmentSessionDto> CancelSessionAsync(Guid id, AdmissionAssessmentActionInput input);
	Task<AdmissionAssessmentDto> GetAsync(Guid applicationId, Guid stageInstanceId);
	Task<AdmissionAssessmentDto> BookAsync(Guid applicationId, Guid stageInstanceId, AdmissionAssessmentBookingInput input);
	Task<AdmissionAssessmentDto> RecordAttendanceAsync(Guid attemptId, AdmissionAssessmentAttendanceInput input);
	Task<AdmissionAssessmentDto> RecordResultAsync(Guid attemptId, AdmissionAssessmentResultInput input);
	Task<AdmissionAssessmentDto> SaveResultDraftAsync(Guid attemptId, AdmissionAssessmentResultInput input);
	Task<AdmissionAssessmentDto> ReviewAsync(Guid attemptId, AdmissionAssessmentReviewInput input);
	Task<AdmissionAssessmentDto> RetakeAsync(Guid attemptId, AdmissionAssessmentActionInput input);
	Task<AdmissionAssessmentDto> CancelAttemptAsync(Guid attemptId, AdmissionAssessmentActionInput input);
	Task<AdmissionAssessmentDto> WaiveAsync(Guid applicationId, Guid stageInstanceId, AdmissionAssessmentWaiverInput input);
}
