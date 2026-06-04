using System;
using FluentValidation;

namespace ShikshaMitra.HR.ProfessionalManagement;

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
	public string FinalRating { get; set; } = string.Empty;
	public string? Strengths { get; set; }
	public string? ImprovementAreas { get; set; }
	public PerformanceRecommendationType Recommendation { get; set; }
	public string? RecommendationNotes { get; set; }
	public bool IncrementRecommended { get; set; }
	public bool PromotionRecommended { get; set; }
}

public class PerformanceOutcomeRequestValidator : AbstractValidator<PerformanceOutcomeRequest>
{
	public PerformanceOutcomeRequestValidator()
	{
		RuleFor(x => x.FinalRating)
			.NotEmpty();
	}
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
	public string Goal { get; set; } = string.Empty;
	public string? ActionPlan { get; set; }
	public Guid? OwnerEmployeeId { get; set; }
	public DateTime? TargetDate { get; set; }
	public DevelopmentPlanStatus Status { get; set; } = DevelopmentPlanStatus.Open;
}

public class DevelopmentPlanRequestValidator : AbstractValidator<DevelopmentPlanRequest>
{
	public DevelopmentPlanRequestValidator()
	{
		RuleFor(x => x.Goal)
			.NotEmpty()
			.Length(2, 240);
	}
}
