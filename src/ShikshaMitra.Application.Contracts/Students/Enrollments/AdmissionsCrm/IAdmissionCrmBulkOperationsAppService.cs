using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmBulkOperationsAppService : IApplicationService
{
	Task<AdmissionCrmBulkOperationResultDto> AssignOwnersAsync(Guid academicYearId, AdmissionCrmBulkAssignOwnersDto input);
	Task<AdmissionCrmBulkOperationResultDto> CreateTasksAsync(Guid academicYearId, AdmissionCrmBulkCreateTasksDto input);
	Task<AdmissionCrmBulkOperationResultDto> MarkExamAttendanceAsync(Guid academicYearId, AdmissionCrmBulkExamAttendanceDto input);
	Task<AdmissionCrmBulkOperationResultDto> UploadExamResultsAsync(Guid academicYearId, AdmissionCrmBulkExamResultDto input);
	Task<AdmissionCrmBulkOperationResultDto> ChangeStageAsync(Guid academicYearId, AdmissionCrmBulkChangeStageDto input);
	Task<AdmissionCrmBulkOperationResultDto> ArchiveAsync(Guid academicYearId, AdmissionCrmBulkArchiveDto input);
	Task<AdmissionCrmBulkOperationResultDto> SendMessagesAsync(Guid academicYearId, AdmissionCrmBulkSendMessageDto input);
	Task<AdmissionCrmBulkOperationResultDto> PreviewOffersAsync(Guid academicYearId, AdmissionCrmBulkOfferDto input);
	Task<AdmissionCrmBulkOperationResultDto> CreateOffersAsync(Guid academicYearId, AdmissionCrmBulkOfferDto input);
	Task<AdmissionCrmBulkOperationResultDto> PreviewActivationsAsync(Guid academicYearId, AdmissionCrmBulkActivationPreviewDto input);
	Task<AdmissionCrmBulkOperationResultDto> ExecuteActivationsAsync(Guid academicYearId, AdmissionCrmBulkExecuteActivationsDto input);
}
