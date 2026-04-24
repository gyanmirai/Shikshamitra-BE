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
/// Supports multi-division assignment model with flexible scheduling
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
	/// <summary>
	/// Legacy single division ID (nullable - for backward compatibility)
	/// New assignments use DivisionSchedules instead
	/// </summary>
	public Guid? DivisionId { get; set; }

	#region IUserScopedEntity Explicit Implementation
	Guid? IUserScopedEntity.AcademicYearId => AcademicYearId;
	Guid? IUserScopedEntity.EducationBoardId => EducationBoardId;
	Guid? IUserScopedEntity.EducationMediumId => EducationMediumId;
	Guid? IUserScopedEntity.EducationMediumStageId => EducationMediumStageId;
	Guid? IUserScopedEntity.StandardId => StandardId;
	Guid? IUserScopedEntity.DivisionId => DivisionId;
	#endregion

	// Core Properties
	public Guid SubjectId { get; set; }

	/// <summary>
	/// Legacy single chapter ID (for backward compatibility)
	/// New assignments use ChapterIdsJson for multi-chapter support
	/// </summary>
	public Guid? ChapterId { get; set; }

	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Legacy homework assignment type ID (nullable - for backward compatibility)
	/// New assignments use HomeworkTypeId
	/// </summary>
	public Guid? HomeworkAssignmentTypeId { get; set; }

	/// <summary>
	/// Legacy single document URL (for backward compatibility)
	/// New assignments use AttachmentsJson for multi-file support
	/// </summary>
	public string? DocumentUrl { get; set; }

	public SubmissionType SubmissionType { get; set; }

	/// <summary>
	/// Legacy due date (nullable - for backward compatibility)
	/// New assignments use division schedules for per-division due dates
	/// </summary>
	public DateTime? DueDate { get; set; }

	/// <summary>
	/// Legacy publish flag (for backward compatibility)
	/// New assignments use division-level IsPublished in schedules
	/// </summary>
	public bool IsPublished { get; set; } = false;

	// Multi-Division Support Properties
	/// <summary>
	/// JSON array of chapter IDs for multi-chapter support: ["guid1", "guid2"]
	/// </summary>
	public string? ChapterIdsJson { get; set; }

	/// <summary>
	/// Reference to HomeworkType entity
	/// </summary>
	public Guid? HomeworkTypeId { get; set; }

	/// <summary>
	/// JSON array of attachments for multi-file support: [{name, url, size}]
	/// </summary>
	public string? AttachmentsJson { get; set; }

	// Navigation properties
	public virtual HomeworkAssignmentType? HomeworkAssignmentType { get; set; }
	public virtual HomeworkType? HomeworkType { get; set; }

	/// <summary>
	/// Multi-division schedules with per-division publish dates and due dates
	/// </summary>
	public virtual ICollection<HomeworkDivisionSchedule> DivisionSchedules { get; set; } =
		new List<HomeworkDivisionSchedule>();

	/// <summary>
	/// Lifecycle status of this homework (Draft → Published → Closed)
	/// </summary>
	public HomeworkStatus Status { get; private set; } = HomeworkStatus.Draft;

	// Helper properties
	public bool HasPublishedDivisions =>
		DivisionSchedules?.Any(d => d.IsPublished) ?? false;
	public bool AllDivisionsPublished =>
		DivisionSchedules?.All(d => d.IsPublished) ?? false;
	public bool IsMultiDivision => DivisionSchedules?.Any() ?? false;

	// Private constructor for EF Core
	private HomeworkAssignmentEntity() { }

	// Legacy Constructor (for backward compatibility)
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

	// Multi-Division Constructor (for new APIs)
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

		// Initialize legacy fields as null (not used in multi-division mode)
		HomeworkAssignmentTypeId = null;
		DivisionId = null;
		DueDate = null;
		IsPublished = false;
	}

	#region Legacy Methods (for backward compatibility)

	public void UpdateLegacyInfo(string title, string description, DateTime dueDate)
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

	#region Multi-Division Methods

	public void UpdateBasicInfo(
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

	/// <summary>
	/// Advance the homework through its lifecycle.
	/// Valid transitions: Draft → Published, Published → Closed.
	/// </summary>
	public void ChangeStatus(HomeworkStatus newStatus)
	{
		var valid = (Status, newStatus) switch
		{
			(HomeworkStatus.Draft, HomeworkStatus.Published) => true,
			(HomeworkStatus.Published, HomeworkStatus.Closed) => true,
			_ => false,
		};

		if (!valid)
		{
			throw new Volo.Abp.BusinessException("ShikshaMitra.Homework:InvalidStatusTransition")
				.WithData("CurrentStatus", Status)
				.WithData("RequestedStatus", newStatus);
		}

		Status = newStatus;
	}

	#endregion
}
