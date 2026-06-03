using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.ProfessionalManagement;

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
	public int CommunicationScore { get; set; }
	public int ProblemSolvingScore { get; set; }
	public string? Comments { get; set; }
	public string? Feedback { get; set; }
}

public class ProfessionalReviewSaveRequestValidator : AbstractValidator<ProfessionalReviewSaveRequest>
{
	public ProfessionalReviewSaveRequestValidator()
	{
		RuleFor(x => x.CommunicationScore)
			.InclusiveBetween(0, 5);
		RuleFor(x => x.ProblemSolvingScore)
			.InclusiveBetween(0, 5);
		RuleFor(x => x.Comments)
			.MaximumLength(2000);
		RuleFor(x => x.Feedback)
			.MaximumLength(2000);
	}
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

public class ReopenReviewRequest
{
	public string Reason { get; set; } = string.Empty;
}

public class ReopenReviewRequestValidator : AbstractValidator<ReopenReviewRequest>
{
	public ReopenReviewRequestValidator()
	{
		RuleFor(x => x.Reason)
			.NotEmpty()
			.Length(3, 500);
	}
}
