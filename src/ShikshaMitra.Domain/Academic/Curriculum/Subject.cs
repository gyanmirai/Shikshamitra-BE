using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class Subject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StandardId { get; set; }  // FK to Standard table
	public virtual Standard Standard { get; set; }  // Navigation Property

	public string SubjectCode { get; set; }
	public string SubjectName { get; set; }

	public Guid? PrimaryTeacherId { get; set; }  // FK to Employee table
	public virtual Employee PrimaryTeacher { get; set; }  // Navigation Property

	public DateTimeOffset? ScheduledDate { get; set; }
	public string SubjectColor { get; set; }
	public string? LearningGoals { get; set; }
	public string? Prerequisites { get; set; }
	public string? Description { get; set; }
	public string? UploadedDocument { get; set; }

	protected Subject()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public Subject(
		Guid id,
		Guid? tenantId,
		string subjectCode,
		string subjectName,
		Guid primaryTeacherId,
		DateTimeOffset scheduledDate,
		string subjectColor,
		string? learningGoals,
		string? prerequisites,
		string? description,
		string? uploadedDocument)
		: base(id)
	{
		TenantId = tenantId;
		SubjectCode = subjectCode;
		SubjectName = subjectName;
		PrimaryTeacherId = primaryTeacherId;
		ScheduledDate = scheduledDate;
		SubjectColor = subjectColor;
		LearningGoals = learningGoals;
		Prerequisites = prerequisites;
		Description = description;
		UploadedDocument = uploadedDocument;
	}
}

// Junction Table for Secondary Teachers
public class SubjectTeacher : Entity<Guid>
{
	public Guid SubjectId { get; set; }
	public virtual Subject Subject { get; set; }

	public Guid TeacherId { get; set; }
	public virtual Employee Teacher { get; set; }

	public SubjectTeacher(Guid subjectId, Guid teacherId)
	{
		SubjectId = subjectId;
		TeacherId = teacherId;
	}

	protected SubjectTeacher()
	{
		/* This constructor is for deserialization / ORM purpose */
	}
}
