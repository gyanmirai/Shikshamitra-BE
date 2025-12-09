using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Homework Assignment Entity
/// PHASE A: Supports both V1 (single division) and V2 (multi-division) models
/// V1 APIs use: DivisionId, DueDate, IsPublished, HomeworkAssignmentTypeId, DocumentUrl
/// V2 APIs use: DivisionSchedules, HomeworkTypeId, ChapterIdsJson, AttachmentsJson
/// </summary>
public class HomeworkAssignmentEntity : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
	public Guid? TenantId { get; set; }

	// IUserScopedEntity properties
	public Guid? AcademicYearId { get; set; }
	public Guid? EducationBoardId { get; set; }
	public Guid? EducationMediumId { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }

	#region IUserScopedEntity Explicit Implementation
	Guid? IUserScopedEntity.AcademicYearId => AcademicYearId;
	Guid? IUserScopedEntity.EducationBoardId => EducationBoardId;
	Guid? IUserScopedEntity.EducationMediumId => EducationMediumId;
	Guid? IUserScopedEntity.EducationMediumStageId => EducationMediumStageId;
	Guid? IUserScopedEntity.StandardId => StandardId;
	Guid? IUserScopedEntity.DivisionId => DivisionId;
	#endregion

	// V1 Properties (Keep for backward compatibility - used by existing APIs)
	public Guid SubjectId { get; set; }
	public Guid? ChapterId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Guid HomeworkAssignmentTypeId { get; set; }
	public string? DocumentUrl { get; set; }
	public SubmissionType SubmissionType { get; set; }
	public DateTime DueDate { get; set; }
	public bool IsPublished { get; set; } = false;

	// V2 Properties (NEW - Phase A additions for multi-division support)
	/// <summary>
	/// JSON array of chapter IDs for V2 multi-chapter support: ["guid1", "guid2"]
	/// NULL means use ChapterId (V1)
	/// </summary>
	public string? ChapterIdsJson { get; set; }

	/// <summary>
	/// Reference to new HomeworkType entity (for V2 APIs)
	/// NULL means use HomeworkAssignmentTypeId (V1)
	/// </summary>
	public Guid? HomeworkTypeId { get; set; }

	/// <summary>
	/// JSON array of attachments for V2 multi-file support: [{name, url, size}]
	/// NULL means use DocumentUrl (V1)
	/// </summary>
	public string? AttachmentsJson { get; set; }

	// Navigation properties
	public virtual HomeworkAssignmentType HomeworkAssignmentType { get; set; } = null!;
	public virtual HomeworkType? HomeworkType { get; set; }

	/// <summary>
	/// Multi-division schedules (V2 only - empty for V1 homework)
	/// </summary>
	public virtual ICollection<HomeworkDivisionSchedule> DivisionSchedules { get; set; } =
		new List<HomeworkDivisionSchedule>();

	// Helper properties
	public bool HasPublishedDivisions =>
		DivisionSchedules?.Any(d => d.IsPublished) ?? false;
	public bool AllDivisionsPublished =>
		DivisionSchedules?.All(d => d.IsPublished) ?? false;
	public bool IsV2Homework => DivisionSchedules?.Any() ?? false;

	// Private constructor for EF Core
	private HomeworkAssignmentEntity() { }

	// V1 Constructor (for existing APIs - backward compatibility)
	public HomeworkAssignmentEntity(
		Guid id,
		Guid standardId,
		Guid divisionId,
		Guid subjectId,
		string title,
		string description,
		Guid homeworkAssignmentTypeId,
		SubmissionType submissionType,
		DateTime dueDate,
		Guid? tenantId = null,
		Guid? academicYearId = null,
		Guid? chapterId = null,
		string? documentUrl = null,
		Guid? educationBoardId = null,
		Guid? educationMediumId = null,
		Guid? educationMediumStageId = null
	)
		: base(id)
	{
		StandardId = standardId;
		DivisionId = divisionId;
		SubjectId = subjectId;
		ChapterId = chapterId;
		Title = title;
		Description = description;
		HomeworkAssignmentTypeId = homeworkAssignmentTypeId;
		DocumentUrl = documentUrl;
		SubmissionType = submissionType;
		DueDate = dueDate;
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		EducationMediumStageId = educationMediumStageId;
		IsPublished = false;
		DivisionSchedules = new List<HomeworkDivisionSchedule>();
	}

	// V2 Constructor (for new multi-division APIs)
	public HomeworkAssignmentEntity(
		Guid id,
		Guid standardId,
		Guid subjectId,
		string title,
		string description,
		Guid homeworkTypeId,
		SubmissionType submissionType,
		Guid? tenantId = null,
		Guid? academicYearId = null,
		string? chapterIdsJson = null,
		string? attachmentsJson = null,
		Guid? educationBoardId = null,
		Guid? educationMediumId = null,
		Guid? educationMediumStageId = null
	)
		: base(id)
	{
		StandardId = standardId;
		SubjectId = subjectId;
		Title = title;
		Description = description;
		HomeworkTypeId = homeworkTypeId;
		ChapterIdsJson = chapterIdsJson;
		AttachmentsJson = attachmentsJson;
		SubmissionType = submissionType;
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		EducationMediumStageId = educationMediumStageId;
		DivisionSchedules = new List<HomeworkDivisionSchedule>();

		// V2 uses default values for V1 fields
		HomeworkAssignmentTypeId = Guid.Empty;
		DivisionId = Guid.Empty;
		DueDate = default;
		IsPublished = false;
	}

	#region V1 Methods (for existing APIs - backward compatibility)

	public void UpdateBasicInfo(string title, string description, DateTime dueDate)
	{
		Title = title;
		Description = description;
		DueDate = dueDate;
	}

	public void UpdateDocument(string? documentUrl)
	{
		DocumentUrl = documentUrl;
	}

	public void Publish()
	{
		IsPublished = true;
	}

	public void Unpublish()
	{
		IsPublished = false;
	}

	#endregion

	#region V2 Methods (for new multi-division APIs)

	public void UpdateBasicInfoV2(
		string title,
		string description,
		string? chapterIdsJson = null,
		string? attachmentsJson = null
	)
	{
		Title = title;
		Description = description;
		ChapterIdsJson = chapterIdsJson;
		AttachmentsJson = attachmentsJson;
	}

	public void AddDivisionSchedule(HomeworkDivisionSchedule schedule)
	{
		if (DivisionSchedules == null)
			DivisionSchedules = new List<HomeworkDivisionSchedule>();

		DivisionSchedules.Add(schedule);
	}

	public void ClearDivisionSchedules()
	{
		DivisionSchedules?.Clear();
	}

	public void PublishAll()
	{
		if (DivisionSchedules != null)
		{
			foreach (var schedule in DivisionSchedules.Where(s => !s.IsPublished))
			{
				schedule.Publish();
			}
		}
	}

	#endregion
}
