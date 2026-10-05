using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.HR.ProfessionalManagement;

internal static class ProfessionalManagementProjection
{
	public static ProfessionalReviewCycleDto ToCycleDto(
		ProfessionalReviewCycle cycle,
		IReadOnlyCollection<PerformanceReviewAssignment> assignments) =>
		new()
		{
			Id = cycle.Id,
			AcademicYearId = cycle.AcademicYearId,
			Name = cycle.Name,
			CycleType = cycle.CycleType,
			PeriodStart = cycle.PeriodStart,
			PeriodEnd = cycle.PeriodEnd,
			SelfReviewDueDate = cycle.SelfReviewDueDate,
			ReviewerDueDate = cycle.ReviewerDueDate,
			FinalizationDueDate = cycle.FinalizationDueDate,
			RatingScaleMax = cycle.RatingScaleMax,
			AllowNotApplicable = cycle.AllowNotApplicable,
			AudienceFilterJson = cycle.AudienceFilterJson,
			ReviewerWorkflowJson = cycle.ReviewerWorkflowJson,
			Status = cycle.Status,
			AssignedCount = assignments.Count(x => x.CycleId == cycle.Id),
			FinalizedCount = assignments.Count(x => x.CycleId == cycle.Id && x.Status == ProfessionalReviewStatus.Finalized)
		};

	public static ProfessionalReviewDto ToReviewDto(
		PerformanceReviewAssignment assignment,
		ProfessionalReviewCycle? cycle,
		Employee? employee,
		PerformanceTemplate? template) =>
		new()
		{
			Id = assignment.Id,
			CycleId = assignment.CycleId,
			CycleName = cycle?.Name ?? "Unknown cycle",
			EmployeeId = assignment.EmployeeId,
			EmployeeName = employee?.EmployeeName ?? "Unknown staff",
			EmployeeIdentifier = employee?.EmployeeCode,
			EmployeeRole = employee?.Role?.Name,
			TemplateId = assignment.TemplateId,
			TemplateName = template?.Name,
			FinalScore = assignment.FinalScore,
			FinalRating = assignment.FinalRating,
			Status = assignment.Status,
			FinalizedAt = assignment.FinalizedAt
		};

	public static PerformanceReviewStageDto ToStageDto(
		PerformanceReviewStage stage,
		IEnumerable<PerformanceReviewResponse> responses) =>
		new()
		{
			Id = stage.Id,
			ReviewAssignmentId = stage.ReviewAssignmentId,
			StageType = stage.StageType,
			ReviewerEmployeeId = stage.ReviewerEmployeeId,
			Status = stage.Status,
			SummaryComment = stage.SummaryComment,
			SubmittedAt = stage.SubmittedAt,
			Responses = responses.Where(r => r.StageId == stage.Id).Select(ToResponseDto).ToList()
		};

	public static PerformanceReviewResponseDto ToResponseDto(PerformanceReviewResponse response) =>
		new()
		{
			Id = response.Id,
			CriterionId = response.CriterionId,
			Rating = response.Rating,
			IsNotApplicable = response.IsNotApplicable,
			Comment = response.Comment
		};

	public static PerformanceOutcomeDto ToOutcomeDto(PerformanceOutcome outcome) =>
		new()
		{
			Id = outcome.Id,
			ReviewAssignmentId = outcome.ReviewAssignmentId,
			FinalScore = outcome.FinalScore,
			FinalRating = outcome.FinalRating,
			Strengths = outcome.Strengths,
			ImprovementAreas = outcome.ImprovementAreas,
			Recommendation = outcome.Recommendation,
			RecommendationNotes = outcome.RecommendationNotes,
			IncrementRecommended = outcome.IncrementRecommended,
			PromotionRecommended = outcome.PromotionRecommended
		};

	public static ClassroomObservationDto ToObservationDto(ClassroomObservation observation) =>
		new()
		{
			Id = observation.Id,
			ReviewAssignmentId = observation.ReviewAssignmentId,
			ObserverEmployeeId = observation.ObserverEmployeeId,
			ObservationDate = observation.ObservationDate,
			ClassName = observation.ClassName,
			SubjectName = observation.SubjectName,
			LessonObjectiveScore = observation.LessonObjectiveScore,
			StudentEngagementScore = observation.StudentEngagementScore,
			ClassroomManagementScore = observation.ClassroomManagementScore,
			AssessmentScore = observation.AssessmentScore,
			Notes = observation.Notes
		};

	public static PerformanceEvidenceDto ToEvidenceDto(PerformanceEvidence evidence) =>
		new()
		{
			Id = evidence.Id,
			ReviewAssignmentId = evidence.ReviewAssignmentId,
			CriterionId = evidence.CriterionId,
			Title = evidence.Title,
			FileName = evidence.FileName,
			Url = evidence.Url,
			Notes = evidence.Notes
		};

	public static PerformanceFeedbackDto ToFeedbackDto(PerformanceFeedback feedback) =>
		new()
		{
			Id = feedback.Id,
			ReviewAssignmentId = feedback.ReviewAssignmentId,
			Source = feedback.Source,
			ProviderName = feedback.ProviderName,
			FeedbackText = feedback.FeedbackText
		};

	public static DevelopmentPlanDto ToDevelopmentPlanDto(DevelopmentPlan plan) =>
		new()
		{
			Id = plan.Id,
			ReviewAssignmentId = plan.ReviewAssignmentId,
			Goal = plan.Goal,
			ActionPlan = plan.ActionPlan,
			OwnerEmployeeId = plan.OwnerEmployeeId,
			TargetDate = plan.TargetDate,
			Status = plan.Status
		};

	public static PerformanceAuditEventDto ToAuditEventDto(PerformanceAuditEvent auditEvent) =>
		new()
		{
			Id = auditEvent.Id,
			ReviewAssignmentId = auditEvent.ReviewAssignmentId,
			EventType = auditEvent.EventType,
			Summary = auditEvent.Summary,
			MetadataJson = auditEvent.MetadataJson,
			CreationTime = auditEvent.CreationTime
		};

}
