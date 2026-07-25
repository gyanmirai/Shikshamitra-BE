using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmLeadAppService : IApplicationService
{
	Task<AdmissionCrmQuickInquiryOptionsDto> GetQuickInquiryOptionsAsync(Guid academicYearId);
	Task<AdmissionCrmLeadDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmLeadDto input);
	Task<AdmissionCrmDirectRegistrationResultDto> CreateDirectRegistrationAsync(
		Guid academicYearId,
		CreateDirectAdmissionCrmRegistrationDto input);
	Task<AdmissionCrmLeadDto> GetAsync(Guid id);
	Task<PagedResultDto<AdmissionCrmLeadDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmLeadListInput input);
	Task<IReadOnlyList<AdmissionCrmLeadDuplicateDto>> PreviewDuplicatesAsync(
		Guid academicYearId,
		AdmissionCrmLeadDuplicatePreviewInput input);
	Task<AdmissionCrmLeadDto> AssignOwnerAsync(Guid leadId, AssignAdmissionCrmLeadOwnerDto input);
	Task<AdmissionCrmLeadDto> ChangePriorityAsync(Guid leadId, ChangeAdmissionCrmLeadPriorityDto input);
	Task<AdmissionCrmLeadDto> IssueRegistrationFormAsync(Guid leadId, IssueAdmissionCrmRegistrationFormDto input);
	Task<AdmissionCrmLeadDto> ChangeStageAsync(Guid leadId, ChangeAdmissionCrmLeadStageDto input);
	Task<AdmissionCrmLeadDto> RecalculateScoreAsync(Guid leadId);
	Task<AdmissionCrmLeadDto> MergeDuplicateAsync(Guid targetLeadId, MergeAdmissionCrmDuplicateLeadDto input);
	Task<AdmissionCrmLeadDto> ArchiveAsync(Guid leadId, ArchiveAdmissionCrmLeadDto input);
	Task<AdmissionCrmLeadDto> ReopenAsync(Guid leadId, ReopenAdmissionCrmLeadDto input);
	Task<AdmissionCrmStaleArchiveRunResultDto> RunStaleArchiveRulesAsync(Guid academicYearId);
	Task<AdmissionCrmStaleArchiveRunResultDto> RunStaleArchiveRulesFromAutomationAsync(Guid academicYearId);
	Task<AdmissionCrmSlaReminderRunResultDto> RunSlaReminderRulesAsync(Guid academicYearId);
	Task<AdmissionCrmSlaReminderRunResultDto> RunSlaReminderRulesFromAutomationAsync(Guid academicYearId);
	Task<AdmissionCrmJourneyRunResultDto> RunJourneyRulesAsync(
		Guid academicYearId,
		RunAdmissionCrmJourneyRulesDto input);
	Task<AdmissionCrmJourneyRunResultDto> RunJourneyRulesFromAutomationAsync(
		Guid academicYearId,
		RunAdmissionCrmJourneyRulesDto input);
	Task TrySendAcknowledgementFromAutomationAsync(
		Guid leadId,
		Guid? templateId,
		Dictionary<string, string>? variables = null);
	Task<AdmissionCrmFollowUpTaskDto> CreateTaskAsync(Guid leadId, CreateAdmissionCrmFollowUpTaskDto input);
	Task<PagedResultDto<AdmissionCrmFollowUpTaskDto>> GetTasksAsync(Guid leadId, PagedAndSortedResultRequestDto input);
	Task<IReadOnlyList<AdmissionCrmApplicantChecklistProgressDto>> GetApplicantChecklistAsync(Guid leadId);
	Task<AdmissionCrmApplicantChecklistProgressDto> UpdateApplicantChecklistAsync(
		Guid leadId,
		string key,
		UpdateAdmissionCrmApplicantChecklistProgressDto input);
	Task<AdmissionCrmTimelineEventDto> AddNoteAsync(Guid leadId, CreateAdmissionCrmLeadNoteDto input);
	Task<AdmissionCrmSlaHoldDto> CreateSlaHoldAsync(Guid leadId, CreateAdmissionCrmSlaHoldDto input);
	Task<AdmissionCrmSlaHoldDto> ResumeSlaHoldAsync(Guid holdId, ResumeAdmissionCrmSlaHoldDto input);
	Task<IReadOnlyList<AdmissionCrmSlaHoldDto>> GetSlaHoldsAsync(Guid leadId);
	Task<AdmissionCrmFollowUpTaskDto> CompleteTaskAsync(Guid taskId, CompleteAdmissionCrmFollowUpTaskDto input);
	Task<PagedResultDto<AdmissionCrmTimelineEventDto>> GetTimelineAsync(
		Guid leadId,
		PagedAndSortedResultRequestDto input);
}
