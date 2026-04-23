using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Assignment Entity - Multi-division support
/// Separate from HomeworkAssignmentEntity to maintain clear separation of concerns
/// </summary>
public class AssignmentEntity : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
	public Guid? TenantId { get; set; }

	// IUserScopedEntity properties
	public Guid? AcademicYearId { get; set; }
	public Guid? EducationBoardId { get; set; }
	public Guid? EducationMediumId { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; } // For single-division compatibility (if needed later)

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
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Guid AssignmentTypeId { get; set; }
	public SubmissionType SubmissionType { get; set; }
	public decimal TotalMarks { get; set; }

	/// <summary>
	/// Lifecycle status of this assignment (Draft → Published → Closed)
	/// </summary>
	public AssignmentStatus Status { get; private set; } = AssignmentStatus.Draft;

	// Multi-division support properties
	/// <summary>
	/// JSON array of chapter IDs: ["guid1", "guid2"]
	/// </summary>
	public string? ChapterIdsJson { get; set; }

	/// <summary>
	/// JSON array of attachments: [{name, url, size}]
	/// </summary>
	public string? AttachmentsJson { get; set; }

	// Navigation properties
	public virtual AssignmentType AssignmentType { get; set; } = null!;

	/// <summary>
	/// Multi-division schedules (one assignment can be assigned to multiple divisions)
	/// </summary>
	public virtual ICollection<AssignmentDivisionSchedule> DivisionSchedules { get; set; } =
		new List<AssignmentDivisionSchedule>();

	// Helper properties
	public bool HasPublishedDivisions =>
		DivisionSchedules?.Any(d => d.IsPublished) ?? false;
	public bool AllDivisionsPublished =>
		DivisionSchedules?.All(d => d.IsPublished) ?? false;

	// Private constructor for EF Core
	private AssignmentEntity() { }

	// Constructor for multi-division assignment
	public AssignmentEntity(
		Guid id,
		Guid standardId,
		Guid subjectId,
		string title,
		string description,
		Guid assignmentTypeId,
		SubmissionType submissionType,
		decimal totalMarks,
		Guid? tenantId = null,
		Guid? academicYearId = null,
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
		AssignmentTypeId = assignmentTypeId;
		AttachmentsJson = attachmentsJson;
		SubmissionType = submissionType;
		TotalMarks = totalMarks;
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		EducationBoardId = educationBoardId;
		EducationMediumId = educationMediumId;
		EducationMediumStageId = educationMediumStageId;
		DivisionSchedules = new List<AssignmentDivisionSchedule>();
		DivisionId = Guid.Empty; // Multi-division model doesn't use single division
	}

	#region Domain Methods

	/// <summary>
	/// Update assignment basic information
	/// </summary>
	public void UpdateBasicInfo(
		string title,
		string description,
		decimal totalMarks,
		string? attachmentsJson = null
	)
	{
		Title = title;
		Description = description;
		TotalMarks = totalMarks;
		AttachmentsJson = attachmentsJson;
	}

	/// <summary>
	/// Add division schedule to assignment
	/// </summary>
	public void AddDivisionSchedule(AssignmentDivisionSchedule schedule)
	{
		if (DivisionSchedules == null)
			DivisionSchedules = new List<AssignmentDivisionSchedule>();

		DivisionSchedules.Add(schedule);
	}

	/// <summary>
	/// Clear all division schedules
	/// </summary>
	public void ClearDivisionSchedules()
	{
		DivisionSchedules?.Clear();
	}

	/// <summary>
	/// Publish all division schedules manually
	/// </summary>
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
	/// Advance the assignment through its lifecycle.
	/// Valid transitions: Draft → Published, Published → Closed.
	/// </summary>
	public void ChangeStatus(AssignmentStatus newStatus)
	{
		var valid = (Status, newStatus) switch
		{
			(AssignmentStatus.Draft, AssignmentStatus.Published) => true,
			(AssignmentStatus.Published, AssignmentStatus.Closed) => true,
			_ => false,
		};

		if (!valid)
		{
			throw new Volo.Abp.BusinessException("ShikshaMitra.Assignment:InvalidStatusTransition")
				.WithData("CurrentStatus", Status)
				.WithData("RequestedStatus", newStatus);
		}

		Status = newStatus;
	}

	#endregion
}
