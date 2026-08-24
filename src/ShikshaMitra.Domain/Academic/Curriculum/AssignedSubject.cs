using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class AssignedSubject : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
	#region Properties
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Gets or sets the ID of the standard (e.g., Grade 1, Grade 2) to which the subject is assigned.
	/// </summary>
	public Guid StandardId { get; set; }

	/// <summary>
	/// Navigation property to the associated Standard entity.
	/// </summary>
	public virtual Standard Standard { get; set; }

	/// <summary>
	/// Gets or sets the ID of the subject (e.g., Math, Science) that is assigned.
	/// </summary>
	public Guid SubjectId { get; set; }

	/// <summary>
	/// Navigation property to the associated Subject entity.
	/// </summary>
	public virtual Subject Subject { get; set; }

	/// <summary>
	/// Gets or sets the ID of the elective subject, if this assignment is for an elective.
	/// </summary>
	public Guid? ElectiveGroupId { get; set; }

	/// <summary>
	/// Navigation property to the associated ElectiveGroup entity.
	/// </summary>
	public ElectiveGroup? ElectiveGroup { get; set; }

	/// <summary>
	/// Unique code representing the subject.
	/// </summary>
	public string SubjectCode { get; set; }

	/// <summary>
	/// Collection of teachers assigned to the subject.
	/// </summary>
	public ICollection<AssignedSubjectTeacher> Teachers { get; set; } = new HashSet<AssignedSubjectTeacher>();

	/// <summary>
	/// Learning goals or objectives for the subject.
	/// </summary>
	public string? LearningGoals { get; set; }

	/// <summary>
	/// Prerequisites required for understanding the subject.
	/// </summary>
	public string? Prerequisites { get; set; }

	/// <summary>
	/// Description providing additional details about the subject.
	/// </summary>
	public string? Description { get; set; }

	public ICollection<AssignedSubjectDocument> Documents { get; set; } =
		new HashSet<AssignedSubjectDocument>();

	/// <summary>
	/// Current status of the subject (e.g., Draft, Published).
	/// </summary>
	public SubjectStatus Status { get; set; } = SubjectStatus.Draft;

	/// <summary>
	/// Type of subject (Core or Elective)
	/// </summary>
	public SubjectType SubjectType { get; set; } = SubjectType.Core;

	/// <summary>
	/// When this assignment was published (null if Draft)
	/// </summary>
	public DateTimeOffset? PublishedAt { get; set; }

	/// <summary>
	/// Number of periods per week allocated for this subject
	/// </summary>
	public int WeeklyPeriods { get; set; } = 0;

	/// <summary>
	/// Difficulty level of the subject for this class
	/// </summary>
	public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Beginner;

	/// <summary>
	/// Subject group or department (e.g., "Science", "Humanities")
	/// </summary>
	public string? SubjectGroup { get; set; }

	/// <summary>
	/// Whether this subject was suggested by AI
	/// </summary>
	public bool IsAiSuggested { get; set; } = false;

	/// <summary>
	/// Whether this subject was copied from a previous academic year
	/// </summary>
	public bool IsCopiedFromPreviousYear { get; set; } = false;

	/// <summary>
	/// Computed setup health score (0-100)
	/// </summary>
	public int SetupHealthScore { get; set; } = 0;

	public ICollection<AssignedSubjectChapter> Chapters { get; set; } =
		new HashSet<AssignedSubjectChapter>();

	Guid? IUserScopedEntity.AcademicYearId => null;
	Guid? IUserScopedEntity.EducationBoardId => null;
	Guid? IUserScopedEntity.EducationMediumId => null;
	Guid? IUserScopedEntity.EducationMediumStageId => null;
	Guid? IUserScopedEntity.StandardId => StandardId;
	Guid? IUserScopedEntity.DivisionId => null;
	#endregion

	#region Constructors
	/// <summary>
	/// Private constructor for ORM and deserialization purposes.
	/// </summary>
	private AssignedSubject()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AssignedSubject"/> class with specified parameters.
	/// </summary>
	public AssignedSubject(
		Guid id,
		Guid? tenantId,
		Guid standardId,
		Guid subjectId,
		string? subjectCode,
		SubjectStatus status = SubjectStatus.Draft,
		SubjectType subjectType = SubjectType.Core,
		Guid? electiveGroupId = null,
		string? learningGoals = null,
		string? prerequisites = null,
		string? description = null,
		int weeklyPeriods = 0,
		DifficultyLevel difficultyLevel = DifficultyLevel.Beginner,
		string? subjectGroup = null,
		bool isAiSuggested = false,
		bool isCopiedFromPreviousYear = false
	)
		: base(id)
	{
		Check.NotNullOrEmpty(subjectCode, nameof(SubjectCode));
		TenantId = tenantId;
		StandardId = standardId;
		SubjectId = subjectId;
		SubjectCode = subjectCode;
		Status = status;
		SubjectType = subjectType;
		LearningGoals = learningGoals;
		Prerequisites = prerequisites;
		Description = description;
		ElectiveGroupId = electiveGroupId;
		WeeklyPeriods = weeklyPeriods;
		DifficultyLevel = difficultyLevel;
		SubjectGroup = subjectGroup;
		IsAiSuggested = isAiSuggested;
		IsCopiedFromPreviousYear = isCopiedFromPreviousYear;

		Teachers = new HashSet<AssignedSubjectTeacher>();
		Documents = new HashSet<AssignedSubjectDocument>();
	}

	#endregion

	#region Methods
	public void ChangeStatus(SubjectStatus status)
	{
		Status = status;
	}

	public void SetPrimaryTeacher(Guid teacherId)
	{
		if (teacherId == Guid.Empty)
		{
			throw new ArgumentException("Primary teacher is required.", nameof(teacherId));
		}

		// find the current primary teacher
		var currentPrimary = Teachers.FirstOrDefault(t =>
			t.TeacherType == SubjectTeacherType.Primary
		);
		if (currentPrimary != null)
		{
			Teachers.Remove(currentPrimary);
		}
		var duplicateSecondary = Teachers
			.Where(t => t.TeacherType == SubjectTeacherType.Secondary && t.TeacherId == teacherId)
			.ToList();
		foreach (var teacher in duplicateSecondary)
		{
			Teachers.Remove(teacher);
		}
		Teachers.Add(new AssignedSubjectTeacher(Id, SubjectId, teacherId, SubjectTeacherType.Primary, TenantId));
	}

	public void RemovePrimaryTeacher()
	{
		var currentPrimary = Teachers.FirstOrDefault(t =>
			t.TeacherType == SubjectTeacherType.Primary
		);
		if (currentPrimary != null)
		{
			Teachers.Remove(currentPrimary);
		}
	}

	public void SetSecondaryTeachers(IEnumerable<Guid> teacherIds)
	{
		var primaryTeacherId = Teachers
			.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary)
			?.TeacherId;
		var secondaryTeacherIds = teacherIds
			.Where(id => id != Guid.Empty)
			.Where(id => !primaryTeacherId.HasValue || id != primaryTeacherId.Value)
			.Distinct()
			.ToList();

		// find the current secondary teachers
		var currentSecondary = Teachers
			.Where(t => t.TeacherType == SubjectTeacherType.Secondary)
			.ToList();
		foreach (var teacher in currentSecondary)
		{
			Teachers.Remove(teacher);
		}
		foreach (var teacherId in secondaryTeacherIds)
		{
			Teachers.Add(
				new AssignedSubjectTeacher(Id, SubjectId, teacherId, SubjectTeacherType.Secondary, TenantId)
			);
		}
	}

	public void AddDocument(Guid id, string blobName, Guid? chapterId = null)
	{
		Documents.Add(new AssignedSubjectDocument(id, Id, chapterId, blobName));
	}

	public void RemoveDocument(Guid id, string blobName)
	{
		var document = Documents.FirstOrDefault(d => d.Id == id && d.BlobName == blobName);
		if (document != null)
		{
			Documents.Remove(document);
		}
	}

	public AssignedSubjectChapter AddChapter(Guid id, int chapterNumber, string chapterName, DateTimeOffset startTime, DateTimeOffset endTime, List<string> documents, string? description = null)
	{
		var chapter = new AssignedSubjectChapter(id, Id, chapterNumber, chapterName, startTime, endTime, description);
		foreach (var document in documents)
		{
			chapter.AddDocument(new AssignedSubjectDocument(Guid.NewGuid(), Id, chapter.Id, document));
		}
		Chapters.Add(chapter);
		return chapter;
	}

	public void RemoveChapter(Guid chapterId)
	{
		var chapter = Chapters.FirstOrDefault(c => c.Id == chapterId);
		if (chapter != null)
		{
			Chapters.Remove(chapter);
		}
	}

	/// <summary>
	/// Recomputes and stores the setup health score based on plan scoring criteria.
	/// Score breakdown: SubjectCode=10, SubjectType=10, PrimaryTeacher=20,
	/// LearningGoals=15, Description=5, Prerequisites=5, Syllabus(chapters)=15, ChaptersAdded=10,
	/// WeeklyPeriods=5, Documents=5 → total 100
	/// </summary>
	public void RecalculateSetupHealth()
	{
		var score = 0;
		if (!string.IsNullOrWhiteSpace(SubjectCode)) score += 10;
		score += 10; // SubjectType is always set (enum), always award
		if (Teachers.Any(t => t.TeacherType == SubjectTeacherType.Primary)) score += 20;
		if (HasMeaningfulContent(LearningGoals)) score += 15;
		if (HasMeaningfulContent(Description)) score += 5;
		if (HasMeaningfulContent(Prerequisites)) score += 5;
		if (Chapters.Count > 0) score += 15;
		if (Chapters.Count >= 3) score += 10;
		if (WeeklyPeriods > 0) score += 5;
		if (Documents.Count > 0) score += 5;
		SetupHealthScore = Math.Min(score, 100);
	}

	private static bool HasMeaningfulContent(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		var text = System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", string.Empty);
		text = text.Replace("&nbsp;", " ").Trim();
		return !string.IsNullOrWhiteSpace(text);
	}
	#endregion
}
