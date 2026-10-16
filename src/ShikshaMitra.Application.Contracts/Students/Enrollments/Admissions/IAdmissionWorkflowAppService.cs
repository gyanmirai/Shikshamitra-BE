using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionWorkflowAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionWorkflowDto>> GetListAsync(AdmissionWorkflowListInput input);
	Task<AdmissionWorkflowDto> GetAsync(Guid id);
	Task<AdmissionWorkflowDto> CreateAsync(CreateUpdateAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> UpdateAsync(Guid id, CreateUpdateAdmissionWorkflowDto input);
	Task DeleteAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionWorkflowDto> CloneAsync(Guid id, CloneAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> CreateNewVersionAsync(Guid id);
	Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid id);
	Task<AdmissionWorkflowDto> PublishAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionWorkflowDto> PublishWithGovernanceAsync(Guid id, PublishAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> SubmitForApprovalAsync(Guid id, SubmitAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> ApproveAsync(Guid id, DecideAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> RejectAsync(Guid id, DecideAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> CreateRollbackDraftAsync(Guid id, RollbackAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> ArchiveAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionWorkflowDto> ReopenAsync(Guid id, string? concurrencyStamp = null);
	Task<List<AdmissionWorkflowDto>> GetVersionHistoryAsync(Guid id);
	Task<AdmissionWorkflowUsageDto> GetUsageAsync(Guid id);
	Task<IReadOnlyList<AdmissionWorkflowStageTypeMetadataDto>> GetStageTypesAsync();
	Task<AdmissionWorkflowGovernanceSettingDto> GetGovernanceSettingsAsync();
	Task<AdmissionWorkflowGovernanceSettingDto> UpdateGovernanceSettingsAsync(UpdateAdmissionWorkflowGovernanceSettingDto input);
	Task<List<AdmissionWorkflowApproverDto>> GetApproversAsync(string? filter = null);
	Task<List<AdmissionWorkflowGovernanceHistoryDto>> GetGovernanceHistoryAsync(Guid id);
	Task<List<AdmissionWorkflowNotificationDto>> GetNotificationsAsync(bool unreadOnly = false);
	Task MarkNotificationReadAsync(Guid notificationId);
}
