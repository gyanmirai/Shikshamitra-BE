using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.ProfessionalManagement;

public interface IProfessionalManagementAppService : IApplicationService
{
	Task<ProfessionalReviewCycleListResponse> GetReviewCyclesAsync(int page = 1, int pageSize = 20, string? search = null);
	Task<ProfessionalReviewCycleDto> CreateReviewCycleAsync(ProfessionalReviewCycleRequest input);
	Task<ProfessionalReviewCycleDto> UpdateReviewCycleAsync(Guid id, ProfessionalReviewCycleRequest input);
	Task<ProfessionalReviewCycleDto> PublishReviewCycleAsync(Guid id);
	Task<ProfessionalReviewCycleDto> AssignEmployeesAsync(Guid id, AssignEmployeesRequest input);
	Task<ProfessionalReviewCycleDto> MoveToCalibrationAsync(Guid id);
	Task<ProfessionalReviewCycleDto> FinalizeCycleAsync(Guid id);
	Task<ProfessionalReviewCycleDto> ArchiveCycleAsync(Guid id);
	Task<PerformanceTemplateListResponse> GetTemplatesAsync(int page = 1, int pageSize = 20, string? search = null, PerformanceStaffCategory? staffCategory = null);
	Task<PerformanceTemplateDto> CreateTemplateAsync(PerformanceTemplateRequest input);
	Task<PerformanceTemplateDto> UpdateTemplateAsync(Guid id, PerformanceTemplateRequest input);
	Task<PerformanceTemplateDto> PublishTemplateAsync(Guid id);
	Task<PerformanceTemplateDto> CloneTemplateAsync(Guid id);
	Task<PerformanceTemplateDto> ArchiveTemplateAsync(Guid id);
	Task<DefaultPerformanceTemplatesResponse> GetDefaultTemplatesAsync();
	Task<ProfessionalReviewListResponse> GetCycleReviewsAsync(Guid id, string? search = null);
	Task<ProfessionalReviewListResponse> GetReviewsAsync(Guid? cycleId = null, Guid? employeeId = null, ProfessionalReviewStatus? status = null, string? search = null);
	Task<ProfessionalReviewDto> GetReviewAsync(Guid id);
	Task<ProfessionalReviewDto> SaveDraftAsync(Guid id, ProfessionalReviewSaveRequest input);
	Task<ProfessionalReviewDto> SubmitAsync(Guid id, ProfessionalReviewSaveRequest input);
	Task<ProfessionalReviewDto> SaveSelfAppraisalAsync(Guid id, PerformanceReviewStageRequest input);
	Task<ProfessionalReviewDto> SaveReviewerStageAsync(Guid id, PerformanceReviewStageRequest input);
	Task<ProfessionalReviewDto> SubmitStageAsync(Guid id, PerformanceReviewStageRequest input);
	Task<ProfessionalReviewDto> ReopenReviewAsync(Guid id, ReopenReviewRequest input);
	Task<PerformanceOutcomeDto> SaveFinalOutcomeAsync(Guid id, PerformanceOutcomeRequest input);
	Task<ProfessionalReviewListResponse> GetEmployeeReviewHistoryAsync(Guid employeeId);
	Task<ClassroomObservationDto> AddObservationAsync(Guid id, ClassroomObservationRequest input);
	Task<List<ClassroomObservationDto>> GetObservationsAsync(Guid id);
	Task<PerformanceEvidenceDto> AddEvidenceAsync(Guid id, PerformanceEvidenceRequest input);
	Task DeleteEvidenceAsync(Guid id);
	Task<PerformanceFeedbackDto> AddFeedbackAsync(Guid id, PerformanceFeedbackRequest input);
	Task<List<PerformanceFeedbackDto>> GetFeedbackAsync(Guid id);
	Task<DevelopmentPlanDto> AddDevelopmentPlanAsync(Guid id, DevelopmentPlanRequest input);
	Task<DevelopmentPlanDto> UpdateDevelopmentPlanAsync(Guid id, DevelopmentPlanRequest input);
	Task<PerformanceDashboardDto> GetDashboardAsync();
	Task<List<RatingDistributionDto>> GetRatingDistributionAsync(Guid? cycleId = null);
	Task<List<PendingActionDto>> GetPendingActionsAsync(Guid? cycleId = null);
	Task<List<DevelopmentPlanDto>> GetTrainingNeedsAsync(Guid? cycleId = null);
	Task<List<PerformanceOutcomeDto>> GetIncrementRecommendationsAsync(Guid? cycleId = null);
	Task<List<PerformanceAuditEventDto>> GetAuditTrailAsync(Guid? reviewId = null);
	Task<byte[]> GetReviewPdfAsync(Guid id);
}
