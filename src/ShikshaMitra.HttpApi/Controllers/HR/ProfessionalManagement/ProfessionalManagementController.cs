using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.ProfessionalManagement;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/professional-management")]
[Authorize]
public class ProfessionalManagementController : AbpControllerBase
{
	private readonly IProfessionalManagementAppService _appService;

	public ProfessionalManagementController(IProfessionalManagementAppService appService)
	{
		_appService = appService;
	}

	[HttpGet("templates")]
	public Task<PerformanceTemplateListResponse> GetTemplatesAsync([FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] PerformanceStaffCategory? staffCategory = null) =>
		_appService.GetTemplatesAsync(page, pageSize, search, staffCategory);

	[HttpPost("templates")]
	[ProducesResponseType(typeof(PerformanceTemplateDto), StatusCodes.Status201Created)]
	public Task<PerformanceTemplateDto> CreateTemplateAsync([FromBody] PerformanceTemplateRequest input) => _appService.CreateTemplateAsync(input);

	[HttpPut("templates/{id}")]
	public Task<PerformanceTemplateDto> UpdateTemplateAsync(Guid id, [FromBody] PerformanceTemplateRequest input) => _appService.UpdateTemplateAsync(id, input);

	[HttpPost("templates/{id}/publish")]
	public Task<PerformanceTemplateDto> PublishTemplateAsync(Guid id) => _appService.PublishTemplateAsync(id);

	[HttpPost("templates/{id}/clone")]
	public Task<PerformanceTemplateDto> CloneTemplateAsync(Guid id) => _appService.CloneTemplateAsync(id);

	[HttpPost("templates/{id}/archive")]
	public Task<PerformanceTemplateDto> ArchiveTemplateAsync(Guid id) => _appService.ArchiveTemplateAsync(id);

	[HttpGet("templates/defaults")]
	public Task<DefaultPerformanceTemplatesResponse> GetDefaultTemplatesAsync() => _appService.GetDefaultTemplatesAsync();

	[HttpGet("review-cycles")]
	public Task<ProfessionalReviewCycleListResponse> GetReviewCyclesAsync([FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20, [FromQuery] string? search = null) =>
		_appService.GetReviewCyclesAsync(page, pageSize, search);

	[HttpPost("review-cycles")]
	public Task<ProfessionalReviewCycleDto> CreateReviewCycleAsync([FromBody] ProfessionalReviewCycleRequest input) => _appService.CreateReviewCycleAsync(input);

	[HttpPut("review-cycles/{id}")]
	public Task<ProfessionalReviewCycleDto> UpdateReviewCycleAsync(Guid id, [FromBody] ProfessionalReviewCycleRequest input) => _appService.UpdateReviewCycleAsync(id, input);

	[HttpPost("review-cycles/{id}/publish")]
	public Task<ProfessionalReviewCycleDto> PublishReviewCycleAsync(Guid id) => _appService.PublishReviewCycleAsync(id);

	[HttpPost("review-cycles/{id}/assign-employees")]
	public Task<ProfessionalReviewCycleDto> AssignEmployeesAsync(Guid id, [FromBody] AssignEmployeesRequest input) => _appService.AssignEmployeesAsync(id, input);

	[HttpPost("review-cycles/{id}/move-to-calibration")]
	public Task<ProfessionalReviewCycleDto> MoveToCalibrationAsync(Guid id) => _appService.MoveToCalibrationAsync(id);

	[HttpPost("review-cycles/{id}/finalize")]
	public Task<ProfessionalReviewCycleDto> FinalizeCycleAsync(Guid id) => _appService.FinalizeCycleAsync(id);

	[HttpPost("review-cycles/{id}/archive")]
	public Task<ProfessionalReviewCycleDto> ArchiveCycleAsync(Guid id) => _appService.ArchiveCycleAsync(id);

	[HttpGet("review-cycles/{id}/reviews")]
	public Task<ProfessionalReviewListResponse> GetCycleReviewsAsync(Guid id, [FromQuery] string? search = null) => _appService.GetCycleReviewsAsync(id, search);

	[HttpGet("reviews")]
	public Task<ProfessionalReviewListResponse> GetReviewsAsync([FromQuery] Guid? cycleId = null, [FromQuery] Guid? employeeId = null, [FromQuery] ProfessionalReviewStatus? status = null, [FromQuery] string? search = null) =>
		_appService.GetReviewsAsync(cycleId, employeeId, status, search);

	[HttpGet("reviews/{id}")]
	public Task<ProfessionalReviewDto> GetReviewAsync(Guid id) => _appService.GetReviewAsync(id);

	[HttpPut("reviews/{id}/draft")]
	public Task<ProfessionalReviewDto> SaveDraftAsync(Guid id, [FromBody] ProfessionalReviewSaveRequest input) => _appService.SaveDraftAsync(id, input);

	[HttpPost("reviews/{id}/submit")]
	public Task<ProfessionalReviewDto> SubmitAsync(Guid id, [FromBody] ProfessionalReviewSaveRequest input) => _appService.SubmitAsync(id, input);

	[HttpPut("reviews/{id}/self-appraisal")]
	public Task<ProfessionalReviewDto> SaveSelfAppraisalAsync(Guid id, [FromBody] PerformanceReviewStageRequest input) => _appService.SaveSelfAppraisalAsync(id, input);

	[HttpPut("reviews/{id}/reviewer-stage")]
	public Task<ProfessionalReviewDto> SaveReviewerStageAsync(Guid id, [FromBody] PerformanceReviewStageRequest input) => _appService.SaveReviewerStageAsync(id, input);

	[HttpPost("reviews/{id}/submit-stage")]
	public Task<ProfessionalReviewDto> SubmitStageAsync(Guid id, [FromBody] PerformanceReviewStageRequest input) => _appService.SubmitStageAsync(id, input);

	[HttpPost("reviews/{id}/reopen")]
	public Task<ProfessionalReviewDto> ReopenReviewAsync(Guid id, [FromBody] ReopenReviewRequest input) => _appService.ReopenReviewAsync(id, input);

	[HttpPost("reviews/{id}/final-outcome")]
	public Task<PerformanceOutcomeDto> SaveFinalOutcomeAsync(Guid id, [FromBody] PerformanceOutcomeRequest input) => _appService.SaveFinalOutcomeAsync(id, input);

	[HttpGet("employees/{employeeId}/review-history")]
	public Task<ProfessionalReviewListResponse> GetEmployeeReviewHistoryAsync(Guid employeeId) => _appService.GetEmployeeReviewHistoryAsync(employeeId);

	[HttpPost("reviews/{id}/observations")]
	public Task<ClassroomObservationDto> AddObservationAsync(Guid id, [FromBody] ClassroomObservationRequest input) => _appService.AddObservationAsync(id, input);

	[HttpGet("reviews/{id}/observations")]
	public Task<List<ClassroomObservationDto>> GetObservationsAsync(Guid id) => _appService.GetObservationsAsync(id);

	[HttpGet("reviews/{id}/evidence")]
	public Task<List<PerformanceEvidenceDto>> GetEvidenceAsync(Guid id) => _appService.GetEvidenceAsync(id);

	[HttpPost("reviews/{id}/evidence")]
	public Task<PerformanceEvidenceDto> AddEvidenceAsync(Guid id, [FromBody] PerformanceEvidenceRequest input) => _appService.AddEvidenceAsync(id, input);

	[HttpDelete("evidence/{id}")]
	public Task DeleteEvidenceAsync(Guid id) => _appService.DeleteEvidenceAsync(id);

	[HttpPost("reviews/{id}/feedback")]
	public Task<PerformanceFeedbackDto> AddFeedbackAsync(Guid id, [FromBody] PerformanceFeedbackRequest input) => _appService.AddFeedbackAsync(id, input);

	[HttpGet("reviews/{id}/feedback")]
	public Task<List<PerformanceFeedbackDto>> GetFeedbackAsync(Guid id) => _appService.GetFeedbackAsync(id);

	[HttpPost("reviews/{id}/development-plan")]
	public Task<DevelopmentPlanDto> AddDevelopmentPlanAsync(Guid id, [FromBody] DevelopmentPlanRequest input) => _appService.AddDevelopmentPlanAsync(id, input);

	[HttpPut("development-plans/{id}")]
	public Task<DevelopmentPlanDto> UpdateDevelopmentPlanAsync(Guid id, [FromBody] DevelopmentPlanRequest input) => _appService.UpdateDevelopmentPlanAsync(id, input);

	[HttpGet("dashboard")]
	public Task<PerformanceDashboardDto> GetDashboardAsync() => _appService.GetDashboardAsync();

	[HttpGet("reports/rating-distribution")]
	public Task<List<RatingDistributionDto>> GetRatingDistributionAsync([FromQuery] Guid? cycleId = null) => _appService.GetRatingDistributionAsync(cycleId);

	[HttpGet("reports/pending-actions")]
	public Task<List<PendingActionDto>> GetPendingActionsAsync([FromQuery] Guid? cycleId = null) => _appService.GetPendingActionsAsync(cycleId);

	[HttpGet("reports/training-needs")]
	public Task<List<DevelopmentPlanDto>> GetTrainingNeedsAsync([FromQuery] Guid? cycleId = null) => _appService.GetTrainingNeedsAsync(cycleId);

	[HttpGet("reports/increment-recommendations")]
	public Task<List<PerformanceOutcomeDto>> GetIncrementRecommendationsAsync([FromQuery] Guid? cycleId = null) => _appService.GetIncrementRecommendationsAsync(cycleId);

	[HttpGet("reports/sqaaf-evidence")]
	public Task<List<PerformanceSqaaEvidenceDto>> GetSqaaEvidenceAsync([FromQuery] Guid? cycleId = null, [FromQuery] PerformanceBoardType? boardType = null, [FromQuery] PerformanceStaffCategory? staffCategory = null, [FromQuery] PerformanceSqaaDomain? sqaaDomain = null) =>
		_appService.GetSqaaEvidenceAsync(cycleId, boardType, staffCategory, sqaaDomain);

	[HttpGet("reports/sqaaf-readiness")]
	public Task<List<PerformanceSqaaReadinessDomainDto>> GetSqaafReadinessAsync([FromQuery] Guid? cycleId = null, [FromQuery] PerformanceBoardType? boardType = null, [FromQuery] PerformanceStaffCategory? staffCategory = null, [FromQuery] PerformanceSqaaDomain? sqaaDomain = null) =>
		_appService.GetSqaafReadinessAsync(cycleId, boardType, staffCategory, sqaaDomain);

	[HttpGet("reports/sqaaf-audit-pack")]
	public Task<PerformanceSqaaAuditPackDto> GetSqaafAuditPackAsync([FromQuery] Guid? cycleId = null, [FromQuery] PerformanceBoardType? boardType = null, [FromQuery] PerformanceStaffCategory? staffCategory = null, [FromQuery] PerformanceSqaaDomain? sqaaDomain = null) =>
		_appService.GetSqaafAuditPackAsync(cycleId, boardType, staffCategory, sqaaDomain);

	[HttpGet("reports/sqaaf-audit-pack/export")]
	public Task<PerformanceSqaaAuditPackExportDto> GetSqaafAuditPackExportAsync([FromQuery] Guid? cycleId = null, [FromQuery] PerformanceBoardType? boardType = null, [FromQuery] PerformanceStaffCategory? staffCategory = null, [FromQuery] PerformanceSqaaDomain? sqaaDomain = null) =>
		_appService.GetSqaafAuditPackExportAsync(cycleId, boardType, staffCategory, sqaaDomain);

	[HttpGet("audit")]
	public Task<List<PerformanceAuditEventDto>> GetAuditTrailAsync([FromQuery] Guid? reviewId = null) => _appService.GetAuditTrailAsync(reviewId);

	[HttpGet("reviews/{id}/pdf")]
	public async Task<FileContentResult> GetReviewPdfAsync(Guid id)
	{
		var bytes = await _appService.GetReviewPdfAsync(id);
		return File(bytes, "application/pdf", $"performance-review-{id}.pdf");
	}
}
