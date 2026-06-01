using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class ProfessionalReviewCycleDto
{
	public Guid Id { get; set; }
	public Guid? AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public PerformanceCycleType CycleType { get; set; }
	public DateTime PeriodStart { get; set; }
	public DateTime PeriodEnd { get; set; }
	public DateTime? SelfReviewDueDate { get; set; }
	public DateTime? ReviewerDueDate { get; set; }
	public DateTime? FinalizationDueDate { get; set; }
	public int RatingScaleMax { get; set; } = 5;
	public bool AllowNotApplicable { get; set; } = true;
	public string? AudienceFilterJson { get; set; }
	public string? ReviewerWorkflowJson { get; set; }
	public ProfessionalReviewCycleStatus Status { get; set; }
	public int AssignedCount { get; set; }
	public int FinalizedCount { get; set; }
}

public class ProfessionalReviewCycleRequest : IValidatableObject
{
	public Guid? AcademicYearId { get; set; }

	[Required]
	[StringLength(120, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	public PerformanceCycleType CycleType { get; set; } = PerformanceCycleType.Annual;

	[Required]
	public DateTime PeriodStart { get; set; }

	[Required]
	public DateTime PeriodEnd { get; set; }

	public DateTime? SelfReviewDueDate { get; set; }
	public DateTime? ReviewerDueDate { get; set; }
	public DateTime? FinalizationDueDate { get; set; }

	[Range(1, 10)]
	public int RatingScaleMax { get; set; } = 5;

	public bool AllowNotApplicable { get; set; } = true;
	public string? AudienceFilterJson { get; set; }
	public string? ReviewerWorkflowJson { get; set; }
	public ProfessionalReviewCycleStatus Status { get; set; } = ProfessionalReviewCycleStatus.Draft;

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (PeriodEnd.Date < PeriodStart.Date)
		{
			yield return new ValidationResult("Period end must be on or after period start.", new[] { nameof(PeriodEnd) });
		}
	}
}

public class ProfessionalReviewCycleListResponse
{
	public int TotalCount { get; set; }
	public List<ProfessionalReviewCycleDto> Items { get; set; } = new();
}

public class PerformanceTemplateDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public PerformanceStaffCategory StaffCategory { get; set; }
	public string? RoleName { get; set; }
	public int RatingScaleMax { get; set; } = 5;
	public bool AllowNotApplicable { get; set; } = true;
	public PerformanceTemplateStatus Status { get; set; }
	public string? Description { get; set; }
	public List<PerformanceTemplateSectionDto> Sections { get; set; } = new();
}

public class PerformanceTemplateSectionDto
{
	public Guid Id { get; set; }
	public Guid TemplateId { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal Weight { get; set; }
	public int DisplayOrder { get; set; }
	public List<PerformanceTemplateCriterionDto> Criteria { get; set; } = new();
}

public class PerformanceTemplateCriterionDto
{
	public Guid Id { get; set; }
	public Guid TemplateId { get; set; }
	public Guid SectionId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal Weight { get; set; }
	public bool EvidenceRequired { get; set; }
	public int DisplayOrder { get; set; }
}

public class PerformanceTemplateRequest
{
	[Required]
	[StringLength(160, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	public PerformanceStaffCategory StaffCategory { get; set; }

	[StringLength(120)]
	public string? RoleName { get; set; }

	[Range(1, 10)]
	public int RatingScaleMax { get; set; } = 5;

	public bool AllowNotApplicable { get; set; } = true;

	[StringLength(1000)]
	public string? Description { get; set; }

	public List<PerformanceTemplateSectionRequest> Sections { get; set; } = new();
}

public class PerformanceTemplateSectionRequest
{
	public Guid? Id { get; set; }

	[Required]
	[StringLength(160, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	[Range(0, 100)]
	public decimal Weight { get; set; }

	public int DisplayOrder { get; set; }
	public List<PerformanceTemplateCriterionRequest> Criteria { get; set; } = new();
}

public class PerformanceTemplateCriterionRequest
{
	public Guid? Id { get; set; }

	[Required]
	[StringLength(200, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	[StringLength(1000)]
	public string? Description { get; set; }

	[Range(0, 100)]
	public decimal Weight { get; set; }

	public bool EvidenceRequired { get; set; }
	public int DisplayOrder { get; set; }
}

public class PerformanceTemplateListResponse
{
	public int TotalCount { get; set; }
	public List<PerformanceTemplateDto> Items { get; set; } = new();
}

public class ProfessionalReviewDto
{
	public Guid Id { get; set; }
	public Guid CycleId { get; set; }
	public string CycleName { get; set; } = string.Empty;
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string? EmployeeIdentifier { get; set; }
	public string? EmployeeRole { get; set; }
	public Guid? ReviewerUserId { get; set; }
	public Guid? TemplateId { get; set; }
	public string? TemplateName { get; set; }
	public decimal? FinalScore { get; set; }
	public string? FinalRating { get; set; }
	public int CommunicationScore { get; set; }
	public int ProblemSolvingScore { get; set; }
	public string? Comments { get; set; }
	public string? Feedback { get; set; }
	public ProfessionalReviewStatus Status { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public DateTime? FinalizedAt { get; set; }
	public PerformanceTemplateDto? Template { get; set; }
	public List<PerformanceReviewStageDto> Stages { get; set; } = new();
	public PerformanceOutcomeDto? Outcome { get; set; }
}

public class ProfessionalReviewListResponse
{
	public int TotalCount { get; set; }
	public List<ProfessionalReviewDto> Items { get; set; } = new();
}

public class ProfessionalReviewSaveRequest
{
	[Range(0, 5)]
	public int CommunicationScore { get; set; }

	[Range(0, 5)]
	public int ProblemSolvingScore { get; set; }

	[StringLength(2000)]
	public string? Comments { get; set; }

	[StringLength(2000)]
	public string? Feedback { get; set; }
}

public class AssignEmployeesRequest
{
	public Guid? TemplateId { get; set; }
	public PerformanceStaffCategory? StaffCategory { get; set; }
	public string? Department { get; set; }
	public string? Designation { get; set; }
	public bool ActiveEmployeesOnly { get; set; } = true;
	public List<Guid> EmployeeIds { get; set; } = new();
}

public class PerformanceReviewStageDto
{
	public Guid Id { get; set; }
	public Guid ReviewAssignmentId { get; set; }
	public PerformanceReviewStageType StageType { get; set; }
	public Guid? ReviewerEmployeeId { get; set; }
	public PerformanceReviewStageStatus Status { get; set; }
	public string? SummaryComment { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public List<PerformanceReviewResponseDto> Responses { get; set; } = new();
}

public class PerformanceReviewResponseDto
{
	public Guid Id { get; set; }
	public Guid CriterionId { get; set; }
	public int? Rating { get; set; }
	public bool IsNotApplicable { get; set; }
	public string? Comment { get; set; }
}

public class PerformanceReviewStageRequest
{
	public PerformanceReviewStageType StageType { get; set; }
	[StringLength(2000)]
	public string? SummaryComment { get; set; }
	public List<PerformanceReviewResponseRequest> Responses { get; set; } = new();
}

public class PerformanceReviewResponseRequest
{
	public Guid CriterionId { get; set; }
	[Range(1, 10)]
	public int? Rating { get; set; }
	public bool IsNotApplicable { get; set; }
	[StringLength(2000)]
	public string? Comment { get; set; }
}

public class ReopenReviewRequest
{
	[Required]
	[StringLength(500, MinimumLength = 3)]
	public string Reason { get; set; } = string.Empty;
}

public class ClassroomObservationDto
{
	public Guid Id { get; set; }
	public Guid ReviewAssignmentId { get; set; }
	public Guid ObserverEmployeeId { get; set; }
	public DateTime ObservationDate { get; set; }
	public string? ClassName { get; set; }
	public string? SubjectName { get; set; }
	public int LessonObjectiveScore { get; set; }
	public int StudentEngagementScore { get; set; }
	public int ClassroomManagementScore { get; set; }
	public int AssessmentScore { get; set; }
	public string? Notes { get; set; }
}

public class ClassroomObservationRequest
{
	public Guid ObserverEmployeeId { get; set; }
	public DateTime ObservationDate { get; set; } = DateTime.UtcNow;
	public string? ClassName { get; set; }
	public string? SubjectName { get; set; }
	[Range(1, 5)]
	public int LessonObjectiveScore { get; set; } = 3;
	[Range(1, 5)]
	public int StudentEngagementScore { get; set; } = 3;
	[Range(1, 5)]
	public int ClassroomManagementScore { get; set; } = 3;
	[Range(1, 5)]
	public int AssessmentScore { get; set; } = 3;
	public string? Notes { get; set; }
}

public class PerformanceEvidenceDto
{
	public Guid Id { get; set; }
	public Guid ReviewAssignmentId { get; set; }
	public Guid? CriterionId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? FileName { get; set; }
	public string? Url { get; set; }
	public string? Notes { get; set; }
}

public class PerformanceEvidenceRequest
{
	public Guid? CriterionId { get; set; }
	[Required]
	[StringLength(160, MinimumLength = 2)]
	public string Title { get; set; } = string.Empty;
	public string? FileName { get; set; }
	public string? Url { get; set; }
	public string? Notes { get; set; }
}

public class PerformanceFeedbackDto
{
	public Guid Id { get; set; }
	public Guid ReviewAssignmentId { get; set; }
	public PerformanceFeedbackSource Source { get; set; }
	public string? ProviderName { get; set; }
	public string FeedbackText { get; set; } = string.Empty;
}

public class PerformanceFeedbackRequest
{
	public PerformanceFeedbackSource Source { get; set; }
	public string? ProviderName { get; set; }
	[Required]
	[StringLength(2000, MinimumLength = 2)]
	public string FeedbackText { get; set; } = string.Empty;
}

public class PerformanceOutcomeDto
{
	public Guid Id { get; set; }
	public Guid ReviewAssignmentId { get; set; }
	public decimal FinalScore { get; set; }
	public string FinalRating { get; set; } = string.Empty;
	public string? Strengths { get; set; }
	public string? ImprovementAreas { get; set; }
	public PerformanceRecommendationType Recommendation { get; set; }
	public string? RecommendationNotes { get; set; }
	public bool IncrementRecommended { get; set; }
	public bool PromotionRecommended { get; set; }
}

public class PerformanceOutcomeRequest
{
	public decimal? FinalScore { get; set; }
	[Required]
	public string FinalRating { get; set; } = string.Empty;
	public string? Strengths { get; set; }
	public string? ImprovementAreas { get; set; }
	public PerformanceRecommendationType Recommendation { get; set; }
	public string? RecommendationNotes { get; set; }
	public bool IncrementRecommended { get; set; }
	public bool PromotionRecommended { get; set; }
}

public class DevelopmentPlanDto
{
	public Guid Id { get; set; }
	public Guid ReviewAssignmentId { get; set; }
	public string Goal { get; set; } = string.Empty;
	public string? ActionPlan { get; set; }
	public Guid? OwnerEmployeeId { get; set; }
	public DateTime? TargetDate { get; set; }
	public DevelopmentPlanStatus Status { get; set; }
}

public class DevelopmentPlanRequest
{
	[Required]
	[StringLength(240, MinimumLength = 2)]
	public string Goal { get; set; } = string.Empty;
	public string? ActionPlan { get; set; }
	public Guid? OwnerEmployeeId { get; set; }
	public DateTime? TargetDate { get; set; }
	public DevelopmentPlanStatus Status { get; set; } = DevelopmentPlanStatus.Open;
}

public class PerformanceDashboardDto
{
	public int ActiveCycles { get; set; }
	public int PendingSelfReviews { get; set; }
	public int PendingReviewerReviews { get; set; }
	public int OverdueReviews { get; set; }
	public int FinalizedReviews { get; set; }
	public int TrainingRecommendations { get; set; }
	public int IncrementRecommendations { get; set; }
}

public class RatingDistributionDto
{
	public string Rating { get; set; } = string.Empty;
	public int Count { get; set; }
}

public class PendingActionDto
{
	public Guid ReviewId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string Action { get; set; } = string.Empty;
	public DateTime? DueDate { get; set; }
}

public class PerformanceAuditEventDto
{
	public Guid Id { get; set; }
	public Guid? ReviewAssignmentId { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string Summary { get; set; } = string.Empty;
	public string? MetadataJson { get; set; }
	public DateTime CreationTime { get; set; }
}

public class DefaultPerformanceTemplatesResponse
{
	public List<PerformanceTemplateRequest> Items { get; set; } = new();
}

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
