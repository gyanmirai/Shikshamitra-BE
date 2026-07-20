using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm/{academicYearId}/bulk")]
public class AdmissionCrmBulkOperationsController(IAdmissionCrmBulkOperationsAppService appService) : AbpControllerBase
{
	[HttpPost("lead-owners")]
	public Task<AdmissionCrmBulkOperationResultDto> AssignOwnersAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkAssignOwnersDto input) => appService.AssignOwnersAsync(academicYearId, input);

	[HttpPost("tasks")]
	public Task<AdmissionCrmBulkOperationResultDto> CreateTasksAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkCreateTasksDto input) => appService.CreateTasksAsync(academicYearId, input);

	[HttpPost("exams/attendance")]
	public Task<AdmissionCrmBulkOperationResultDto> MarkExamAttendanceAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkExamAttendanceDto input) => appService.MarkExamAttendanceAsync(academicYearId, input);

	[HttpPost("exams/results")]
	public Task<AdmissionCrmBulkOperationResultDto> UploadExamResultsAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkExamResultDto input) => appService.UploadExamResultsAsync(academicYearId, input);

	[HttpPost("stages")]
	public Task<AdmissionCrmBulkOperationResultDto> ChangeStageAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkChangeStageDto input) => appService.ChangeStageAsync(academicYearId, input);

	[HttpPost("archive")]
	public Task<AdmissionCrmBulkOperationResultDto> ArchiveAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkArchiveDto input) => appService.ArchiveAsync(academicYearId, input);

	[HttpPost("messages")]
	public Task<AdmissionCrmBulkOperationResultDto> SendMessagesAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkSendMessageDto input) => appService.SendMessagesAsync(academicYearId, input);

	[HttpPost("offers/preview")]
	public Task<AdmissionCrmBulkOperationResultDto> PreviewOffersAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkOfferDto input) => appService.PreviewOffersAsync(academicYearId, input);

	[HttpPost("offers")]
	public Task<AdmissionCrmBulkOperationResultDto> CreateOffersAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkOfferDto input) => appService.CreateOffersAsync(academicYearId, input);

	[HttpPost("activations/preview")]
	public Task<AdmissionCrmBulkOperationResultDto> PreviewActivationsAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkActivationPreviewDto input) => appService.PreviewActivationsAsync(academicYearId, input);

	[HttpPost("activations/execute")]
	public Task<AdmissionCrmBulkOperationResultDto> ExecuteActivationsAsync(Guid academicYearId, [FromBody] AdmissionCrmBulkExecuteActivationsDto input) => appService.ExecuteActivationsAsync(academicYearId, input);
}
