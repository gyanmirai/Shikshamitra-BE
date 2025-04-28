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
    #region Properties  
    public Guid? TenantId { get; private set; }
   	public Guid StandardId { get; set; }
	[ForeignKey(nameof(StandardId))]
	public virtual Standard Standard { get; set; }
	public string SubjectCode { get; set; }
	public string SubjectName { get; set; }
	public ICollection<SubjectTeacher> Teachers { get; set; } = new HashSet<SubjectTeacher>();
	public DateTimeOffset ScheduledDate { get; set; }
	public string SubjectColor { get; set; }
	public string? LearningGoals { get; set; }
	public string? Prerequisites { get; set; }
	public string? Description { get; set; }
	public string? UploadedDocument { get; set; }
	public SubjectStatus Status { get; set; } = SubjectStatus.Draft;
	#endregion

	#region Contructors
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
		DateTimeOffset scheduledDate,
		string subjectColor,
		string? learningGoals,
		string? prerequisites,
		string? description,
		SubjectStatus status)
		: base(id)
	{
		TenantId = tenantId;
		YearId = yearId;
		BoardId = boardId;
		MediumId = mediumId;
		StandardId = standardId;
		SubjectCode = Check.NotNullOrEmpty(subjectCode, nameof(SubjectCode));
		SubjectName = Check.NotNullOrEmpty(subjectName, nameof(SubjectName));
		ScheduledDate = scheduledDate;
		SubjectColor = subjectColor;
		LearningGoals = learningGoals;
		Prerequisites = prerequisites;
		Description = description;
		Status = status;
	}
	#endregion

	#region Methods
	public void AddTeacher(SubjectTeacher teacher)
	{
		if (teacher == null) throw new ArgumentNullException(nameof(teacher));
		Teachers.Add(teacher);
	}

	public void RemoveTeacher(SubjectTeacher teacher)
	{
		if (teacher == null) throw new ArgumentNullException(nameof(teacher));
		Teachers.Remove(teacher);
	}

	public void ChangeStatus(SubjectStatus status)
	{
		Status = status;
	}
	#endregion
}

public class SubjectTeacher : Entity<Guid>
{
	#region Properties
	public Guid SubjectId { get; private set; }
	[ForeignKey(nameof(SubjectId))]
	public virtual Subject Subject { get; private set; }
	public Guid TeacherId { get; private set; }
	[ForeignKey(nameof(TeacherId))]
	public virtual Employee Teacher { get; private set; }
	public TeacherType TeacherType { get; set; }
	#endregion

	#region Constructors
	private SubjectTeacher() { }

	public SubjectTeacher(Guid id, Guid subjectId, Guid teacherId, TeacherType teacherType) : base(id)
	{
		SubjectId = subjectId;
		TeacherId = teacherId;
		TeacherType = teacherType;
	}
	#endregion
}
