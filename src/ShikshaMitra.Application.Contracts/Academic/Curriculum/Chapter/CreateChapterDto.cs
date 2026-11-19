using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for creating a chapter
/// Aligned with FE contract
/// </summary>
public class CreateChapterDto : IValidatableObject
{
	public Guid AssignedSubjectId { get; set; }
	/// <summary>
	/// Chapter number (e.g., 1, 2, 3...)
	/// Must be unique per subject
	/// </summary>
	[Required(ErrorMessage = "Chapter Number is required")]
	[Range(ChapterConsts.MinChapterNumber, ChapterConsts.MaxChapterNumber, ErrorMessage = "Chapter Number must be between 1 and 1000")]
	public int ChapterNo { get; set; }

	/// <summary>
	/// Chapter name/title
	/// </summary>
	[Required(ErrorMessage = "Chapter Name is required")]
	[StringLength(ChapterConsts.MaxChapterNameLength, MinimumLength = ChapterConsts.MinChapterNameLength, ErrorMessage = "Chapter name must be between {2} and {1} characters")]
	public string ChapterName { get; set; } = null!; // The response mapper populates these members after construction.

	/// <summary>
	/// Chapter description (HTML content)
	/// </summary>
	[StringLength(ChapterConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	/// <summary>
	/// Chapter start date (must be within academic year)
	/// Format: YYYY-MM-DD
	/// </summary>
	[Required(ErrorMessage = "Start Date is required")]
	public DateTimeOffset StartDate { get; set; }

	/// <summary>
	/// Chapter end date (must be >= start date and within academic year)
	/// Format: YYYY-MM-DD
	/// </summary>
	[Required(ErrorMessage = "End Date is required")]
	public DateTimeOffset EndDate { get; set; }

	/// <summary>
	/// Pre-uploaded document URLs (if using JSON approach)
	/// </summary>
	public List<string>? Documents { get; set; }

	public string? UnitName { get; set; }
	public string? TermName { get; set; }
	public string? Topic { get; set; }
	public int? EstimatedTeachingDays { get; set; }
	public int? EstimatedPeriods { get; set; }
	public int? RevisionDays { get; set; }
	public int? BufferDays { get; set; }
	public string? DifficultyLevel { get; set; }
	public string? BloomLevel { get; set; }
	public string? Tags { get; set; }
	public string? LearningOutcomes { get; set; }
	public string? SkillsCovered { get; set; }
	public string? Prerequisites { get; set; }
	public string? TeachingMethods { get; set; }
	public string? ClassActivities { get; set; }
	public string? CompletionCriteria { get; set; }
	public string? TeacherNotes { get; set; }
	public string? StudentSummary { get; set; }
	public string? ParentSummary { get; set; }
	public string? ReferenceLinks { get; set; }
	public string? Worksheets { get; set; }
	public string? AudioVideoMaterials { get; set; }
	public string? BoardDocuments { get; set; }
	public string? AssessmentMethod { get; set; }
	public string? QuestionTypes { get; set; }
	public int? MarksWeightage { get; set; }
	public string? BloomCoverage { get; set; }
	public string? DifficultyDistribution { get; set; }
	public string? Visibility { get; set; }
	public string? PublishStatus { get; set; }
	public string? ReviewStatus { get; set; }
	public bool NotifyOnPublish { get; set; }
	public int ProgressPercentage { get; set; }
	public string? CoverageStatus { get; set; }
	public string? LessonPlanStatus { get; set; }
	public string? HomeworkStatus { get; set; }
	public string? AssessmentStatus { get; set; }
	public string? ResourceStatus { get; set; }
	public int QuestionCount { get; set; }
	public int HealthScore { get; set; }
	public bool IsAiGenerated { get; set; }
	public bool CopiedFromPreviousYear { get; set; }
	public string? TemplateSource { get; set; }
	public string? DependencyChapterIds { get; set; }
	public string? VersionLabel { get; set; }

	/// <summary>
	/// Validates the DTO, ensuring cross-property constraints are met
	/// </summary>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (EndDate < StartDate)
		{
			yield return new ValidationResult(
				"End Date must be greater than or equal to Start Date",
				new[] { nameof(EndDate) }
			);
		}
	}
}
