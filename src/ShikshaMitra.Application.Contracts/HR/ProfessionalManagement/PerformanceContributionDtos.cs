using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.ProfessionalManagement;

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
	public DateTime ObservationDate { get; set; }
	public string? ClassName { get; set; }
	public string? SubjectName { get; set; }
	public int LessonObjectiveScore { get; set; } = 3;
	public int StudentEngagementScore { get; set; } = 3;
	public int ClassroomManagementScore { get; set; } = 3;
	public int AssessmentScore { get; set; } = 3;
	public string? Notes { get; set; }
}

public class ClassroomObservationRequestValidator : AbstractValidator<ClassroomObservationRequest>
{
	public ClassroomObservationRequestValidator()
	{
		RuleFor(x => x.LessonObjectiveScore)
			.InclusiveBetween(1, 5);
		RuleFor(x => x.StudentEngagementScore)
			.InclusiveBetween(1, 5);
		RuleFor(x => x.ClassroomManagementScore)
			.InclusiveBetween(1, 5);
		RuleFor(x => x.AssessmentScore)
			.InclusiveBetween(1, 5);
	}
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
	public string Title { get; set; } = string.Empty;
	public string? FileName { get; set; }
	public string? Url { get; set; }
	public string? Notes { get; set; }
}

public class PerformanceEvidenceRequestValidator : AbstractValidator<PerformanceEvidenceRequest>
{
	public PerformanceEvidenceRequestValidator()
	{
		RuleFor(x => x.Title)
			.NotEmpty()
			.Length(2, 160);
	}
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
	public string FeedbackText { get; set; } = string.Empty;
}

public class PerformanceFeedbackRequestValidator : AbstractValidator<PerformanceFeedbackRequest>
{
	public PerformanceFeedbackRequestValidator()
	{
		RuleFor(x => x.FeedbackText)
			.NotEmpty()
			.Length(2, 2000);
	}
}
