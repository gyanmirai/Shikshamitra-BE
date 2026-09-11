using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionReviewAppService : IApplicationService
{
	Task<AdmissionReviewDto> GetAsync(Guid applicationId, Guid stageId);
	Task<List<AdmissionReviewRecordDto>> GetHistoryAsync(Guid applicationId, Guid stageId);
	Task<AdmissionReviewDto> SaveDraftAsync(Guid applicationId, Guid stageId, AdmissionReviewSaveDto input);
	Task<AdmissionReviewDto> SubmitAsync(Guid applicationId, Guid stageId, AdmissionReviewSaveDto input);
	Task<AdmissionReviewDto> AssignAsync(Guid applicationId, Guid stageId, AdmissionReviewAssignmentDto input);
	Task<AdmissionReviewDto> RequestCorrectionAsync(Guid applicationId, Guid stageId, AdmissionReviewCorrectionDto input);
	Task<PagedResultDto<AdmissionReviewQueueItemDto>> GetQueueAsync(AdmissionReviewQueueInput input);
	Task<List<AdmissionEligibilityRoleDto>> GetReviewerRolesAsync();
	Task<List<AdmissionReviewUserDto>> GetReviewersAsync(string? filter = null, string? role = null);
}
