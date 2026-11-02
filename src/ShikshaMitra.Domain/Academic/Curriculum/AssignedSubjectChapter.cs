using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Represents a chapter in a subject for a specific standard.
/// </summary>
public class AssignedSubjectChapter : FullAuditedEntity<Guid>, IMultiTenant
{
	#region Properties
	/// <summary>
	/// Gets or sets the tenant ID.
	/// </summary>
	public Guid? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the ID of the subject (e.g., Math, Science).
	/// </summary>
	public Guid AssignedSubjectId { get; set; }

	/// <summary>
	/// Gets or sets the chapter number (e.g., 1, 2, 3).
	/// </summary>
	public int ChapterNumber { get; set; }

	/// <summary>
	/// Gets or sets the name/title of the chapter.
	/// </summary>
	public string ChapterName { get; set; }

	/// <summary>
	/// Gets or sets the planned start time of the chapter.
	/// </summary>
	public DateTimeOffset ChapterStartTime { get; set; }

	/// <summary>
	/// Gets or sets the planned end time of the chapter.
	/// </summary>
	public DateTimeOffset ChapterEndTime { get; set; }

	/// <summary>
	/// Gets or sets an optional description of the chapter (e.g., objectives, summary).
	/// </summary>
	public string? Description { get; set; }

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
	/// Navigation property for chapter documents
	/// </summary>
	public ICollection<AssignedSubjectDocument> Documents { get; set; } = new List<AssignedSubjectDocument>();

	#endregion

	#region Constructors

	// Default constructor
	protected AssignedSubjectChapter()
	{
		// EF materializes these required columns after this ORM constructor.
		ChapterName = null!;
	}

	// Parameterized constructor
	public AssignedSubjectChapter(
		Guid id,
		Guid assignedSubjectId,
		int chapterNumber,
		string chapterName,
		DateTimeOffset chapterStartTime,
		DateTimeOffset chapterEndTime,
		string? description = null
	)
		: base(id)
	{
		ChapterNumber = ValidateChapterNumber(chapterNumber);
		ChapterName = NormalizeChapterName(chapterName);
		ChapterStartTime = chapterStartTime;
		ChapterEndTime = chapterEndTime;
		Description = description;
		Documents = new List<AssignedSubjectDocument>();
	}

	#endregion

	#region Methods

	public void Update(
		string chapterName,
		DateTimeOffset chapterStartTime,
		DateTimeOffset chapterEndTime,
		string? description = null
	)
	{
		ChapterName = NormalizeChapterName(chapterName);
		ChapterStartTime = chapterStartTime;
		ChapterEndTime = chapterEndTime;
		Description = description;
	}

	/// <summary>
	/// Updates the chapter dates
	/// </summary>
	public void UpdateDates(DateTimeOffset startTime, DateTimeOffset endTime)
	{
		ChapterStartTime = startTime;
		ChapterEndTime = endTime;
	}

	/// <summary>
	/// Updates chapter details
	/// </summary>
	public void UpdateDetails(int chapterNumber, string chapterName, string? description)
	{
		ChapterNumber = ValidateChapterNumber(chapterNumber);
		ChapterName = NormalizeChapterName(chapterName);
		Description = description;
	}

	private static string NormalizeChapterName(string chapterName)
	{
		var normalizedChapterName = Check.NotNullOrWhiteSpace(chapterName, nameof(chapterName)).Trim();
		if (normalizedChapterName.Length < ChapterConsts.MinChapterNameLength)
		{
			throw new ArgumentException(
				$"Chapter name must be at least {ChapterConsts.MinChapterNameLength} characters.",
				nameof(chapterName));
		}

		return Check.Length(
			normalizedChapterName,
			nameof(chapterName),
			ChapterConsts.MaxChapterNameLength)!;
	}

	private static int ValidateChapterNumber(int chapterNumber)
	{
		if (chapterNumber < ChapterConsts.MinChapterNumber || chapterNumber > ChapterConsts.MaxChapterNumber)
		{
			throw new ArgumentOutOfRangeException(
				nameof(chapterNumber),
				chapterNumber,
				$"Chapter Number must be between {ChapterConsts.MinChapterNumber} and {ChapterConsts.MaxChapterNumber}.");
		}

		return chapterNumber;
	}

	public void UpdatePlanningDetails(
		string? unitName,
		string? termName,
		string? topic,
		int? estimatedTeachingDays,
		int? estimatedPeriods,
		int? revisionDays,
		int? bufferDays,
		string? difficultyLevel,
		string? bloomLevel,
		string? tags,
		string? learningOutcomes,
		string? skillsCovered,
		string? prerequisites,
		string? teachingMethods,
		string? classActivities,
		string? completionCriteria,
		string? teacherNotes,
		string? studentSummary,
		string? parentSummary,
		string? referenceLinks,
		string? worksheets,
		string? audioVideoMaterials,
		string? boardDocuments,
		string? assessmentMethod,
		string? questionTypes,
		int? marksWeightage,
		string? bloomCoverage,
		string? difficultyDistribution,
		string? visibility,
		string? publishStatus,
		string? reviewStatus,
		bool notifyOnPublish,
		int progressPercentage,
		string? coverageStatus,
		string? lessonPlanStatus,
		string? homeworkStatus,
		string? assessmentStatus,
		string? resourceStatus,
		int questionCount,
		int healthScore,
		bool isAiGenerated,
		bool copiedFromPreviousYear,
		string? templateSource,
		string? dependencyChapterIds,
		string? versionLabel
	)
	{
		UnitName = unitName;
		TermName = termName;
		Topic = topic;
		EstimatedTeachingDays = estimatedTeachingDays;
		EstimatedPeriods = estimatedPeriods;
		RevisionDays = revisionDays;
		BufferDays = bufferDays;
		DifficultyLevel = difficultyLevel;
		BloomLevel = bloomLevel;
		Tags = tags;
		LearningOutcomes = learningOutcomes;
		SkillsCovered = skillsCovered;
		Prerequisites = prerequisites;
		TeachingMethods = teachingMethods;
		ClassActivities = classActivities;
		CompletionCriteria = completionCriteria;
		TeacherNotes = teacherNotes;
		StudentSummary = studentSummary;
		ParentSummary = parentSummary;
		ReferenceLinks = referenceLinks;
		Worksheets = worksheets;
		AudioVideoMaterials = audioVideoMaterials;
		BoardDocuments = boardDocuments;
		AssessmentMethod = assessmentMethod;
		QuestionTypes = questionTypes;
		MarksWeightage = marksWeightage;
		BloomCoverage = bloomCoverage;
		DifficultyDistribution = difficultyDistribution;
		Visibility = visibility;
		PublishStatus = publishStatus;
		ReviewStatus = reviewStatus;
		NotifyOnPublish = notifyOnPublish;
		ProgressPercentage = Math.Clamp(progressPercentage, 0, 100);
		CoverageStatus = coverageStatus;
		LessonPlanStatus = lessonPlanStatus;
		HomeworkStatus = homeworkStatus;
		AssessmentStatus = assessmentStatus;
		ResourceStatus = resourceStatus;
		QuestionCount = Math.Max(questionCount, 0);
		HealthScore = Math.Clamp(healthScore, 0, 100);
		IsAiGenerated = isAiGenerated;
		CopiedFromPreviousYear = copiedFromPreviousYear;
		TemplateSource = templateSource;
		DependencyChapterIds = dependencyChapterIds;
		VersionLabel = versionLabel;
	}

	/// <summary>
	/// Adds a document to the chapter
	/// </summary>
	public void AddDocument(AssignedSubjectDocument document)
	{
		Documents ??= new List<AssignedSubjectDocument>();
		Documents.Add(document);
	}

	/// <summary>
	/// Removes a document from the chapter
	/// </summary>
	public void RemoveDocument(AssignedSubjectDocument document)
	{
		Documents?.Remove(document);
	}
	#endregion
}
