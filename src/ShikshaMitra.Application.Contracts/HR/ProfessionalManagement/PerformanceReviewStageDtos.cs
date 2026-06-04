using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.ProfessionalManagement;

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
	public string? SummaryComment { get; set; }
	public List<PerformanceReviewResponseRequest> Responses { get; set; } = new();
}

public class PerformanceReviewStageRequestValidator : AbstractValidator<PerformanceReviewStageRequest>
{
	public PerformanceReviewStageRequestValidator()
	{
		RuleFor(x => x.SummaryComment)
			.MaximumLength(2000);
		RuleForEach(x => x.Responses)
			.SetValidator(new PerformanceReviewResponseRequestValidator());
	}
}

public class PerformanceReviewResponseRequest
{
	public Guid CriterionId { get; set; }
	public int? Rating { get; set; }
	public bool IsNotApplicable { get; set; }
	public string? Comment { get; set; }
}

public class PerformanceReviewResponseRequestValidator : AbstractValidator<PerformanceReviewResponseRequest>
{
	public PerformanceReviewResponseRequestValidator()
	{
		RuleFor(x => x.Rating)
			.InclusiveBetween(1, 10)
			.When(x => x.Rating.HasValue);
		RuleFor(x => x.Comment)
			.MaximumLength(2000);
	}
}
