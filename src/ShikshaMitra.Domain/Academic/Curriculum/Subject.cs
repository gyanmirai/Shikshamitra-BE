using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Values;
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

	public ICollection<SubjectTeacher>? SecondaryTeachers { get; set; }

	private Subject()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public Subject(
		Guid id,
		Guid? tenantId,
		Guid standardId,
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
		StandardId = standardId;
		SubjectCode = Check.NotNullOrEmpty(subjectCode, nameof(SubjectCode));
		SubjectName = Check.NotNullOrEmpty(subjectName, nameof(SubjectName));
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
public class SubjectTeacher : ValueObject
{
	public Guid SubjectId { get; set; }  // FK to Subject table
	public virtual Subject Subject { get; set; }  // Navigation Property
	public Guid SecondaryTeacherId { get; set; }  // FK to Employee table
	public virtual Employee SecondaryTeacher { get; set; }  // Navigation Property
	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return SubjectId;
		yield return SecondaryTeacherId;
	}
}
