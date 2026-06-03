using System;
using System.Collections.Generic;
using FluentValidation;

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

public class ProfessionalReviewCycleRequest
{
	public Guid? AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public PerformanceCycleType CycleType { get; set; } = PerformanceCycleType.Annual;
	public DateTime PeriodStart { get; set; }
	public DateTime PeriodEnd { get; set; }
	public DateTime? SelfReviewDueDate { get; set; }
	public DateTime? ReviewerDueDate { get; set; }
	public DateTime? FinalizationDueDate { get; set; }
	public int RatingScaleMax { get; set; } = 5;
	public bool AllowNotApplicable { get; set; } = true;
	public string? AudienceFilterJson { get; set; }
	public string? ReviewerWorkflowJson { get; set; }
	public ProfessionalReviewCycleStatus Status { get; set; } = ProfessionalReviewCycleStatus.Draft;
}

public class ProfessionalReviewCycleRequestValidator : AbstractValidator<ProfessionalReviewCycleRequest>
{
	public ProfessionalReviewCycleRequestValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.Length(2, 120);
		RuleFor(x => x.RatingScaleMax)
			.InclusiveBetween(1, 10);
		RuleFor(x => x.PeriodEnd)
			.Must((request, periodEnd) => periodEnd.Date >= request.PeriodStart.Date)
			.WithMessage("Period end must be on or after period start.");
	}
}

public class ProfessionalReviewCycleListResponse
{
	public int TotalCount { get; set; }
	public List<ProfessionalReviewCycleDto> Items { get; set; } = new();
}
