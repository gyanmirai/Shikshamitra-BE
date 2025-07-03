using System;
using ShikshaMitra.Academic.Homework;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkAssignmentEntity : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? ChapterId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Guid HomeworkAssignmentTypeId { get; set; }
	public string? DocumentUrl { get; set; }
	public SubmissionType SubmissionType { get; set; }
	public DateTime DueDate { get; set; }
	public bool IsPublished { get; set; } = false;

	// Navigation properties
	public virtual HomeworkAssignmentType HomeworkAssignmentType { get; set; } = null!;

	// Private constructor for EF Core
	private HomeworkAssignmentEntity() { }

	// Public constructor
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
		Guid? chapterId = null,
		string? documentUrl = null
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
		IsPublished = false;
	}

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
}
