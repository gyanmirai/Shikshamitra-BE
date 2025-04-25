using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class Subject : GyanMiraiFullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StandardId { get; set; }  // FK to Standard table
	public virtual Standard Standard { get; set; }  // Navigation Property

	public string SubjectCode { get; set; }
	public string SubjectName { get; set; }

	public Guid? PrimaryTeacherId { get; set; }  // FK to Employee table
	public virtual Employee PrimaryTeacher { get; set; }  // Navigation Property
	public ICollection<SubjectTeacher>? SecondaryTeachers { get; set; }

	public DateTimeOffset? ScheduledDate { get; set; }
	public string SubjectColor { get; set; }
	public string? LearningGoals { get; set; }
	public string? Prerequisites { get; set; }
	public string? Description { get; set; }
	public string? UploadedDocument { get; set; }
	
	private Subject()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public Subject(
		Guid id,
		Guid? tenantId,
		Guid yearId,
		Guid boardId,
		Guid mediumId,
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
		YearId = yearId;
		BoardId = boardId;
		MediumId = mediumId;
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
		SecondaryTeachers = new HashSet<SubjectTeacher>();
	}

    // create method to add a single secondary teacher
    public void AddSecondaryTeacher(Guid subjectTeacherId, Guid teacherId)
    {
        if (SecondaryTeachers == null)
        {
            SecondaryTeachers = new HashSet<SubjectTeacher>();
        }

        var subjectTeacher = new SubjectTeacher(subjectTeacherId, Id, teacherId);
        SecondaryTeachers.Add(subjectTeacher);
    }
}

public class SubjectTeacher : Entity<Guid>
{
	public Guid SubjectId { get; private set; }
	public Guid TeacherId { get; private set; }

	[ForeignKey(nameof(SubjectId))]
	public virtual Subject Subject { get; private set; }

	[ForeignKey(nameof(TeacherId))]
	public virtual Employee Teacher { get; private set; }

	private SubjectTeacher() { }

	public SubjectTeacher(Guid id, Guid subjectId, Guid teacherId) : base(id)
	{
		SubjectId = subjectId;
		TeacherId = teacherId;
	}
}
