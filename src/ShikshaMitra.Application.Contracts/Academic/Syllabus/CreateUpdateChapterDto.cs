using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Syllabus;

public class CreateUpdateChapterDto
{
	public Guid AssignedSubjectId { get; set; }
	public int ChapterNo { get; set; }
	public string ChapterName { get; set; } = string.Empty;
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
	public string? Description { get; set; }
	public List<string> Documents { get; set; } = new List<string>();
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
}
