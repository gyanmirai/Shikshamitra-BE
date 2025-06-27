using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class StandardSubjectAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
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
	public Guid? ElectiveSubjectId { get; set; }

	/// <summary>
	/// Navigation property to the associated ElectiveSubject entity.
	/// </summary>
	public ElectiveSubject? ElectiveSubject { get; set; }

	/// <summary>
	/// Unique code representing the subject.
	/// </summary>
	public string SubjectCode { get; set; }

	/// <summary>
	/// Collection of teachers assigned to the subject.
	/// </summary>
	public ICollection<SubjectTeacher> Teachers { get; set; } = new HashSet<SubjectTeacher>();

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

	/// <summary>
	/// Path or reference to the uploaded document related to the subject.
	/// </summary>
	public string? UploadedDocument { get; set; }

	/// <summary>
	/// Current status of the subject (e.g., Draft, Published).
	/// </summary>
	public SubjectStatus Status { get; set; } = SubjectStatus.Draft;
	#endregion

	#region Constructors
	/// <summary>
	/// Private constructor for ORM and deserialization purposes.
	/// </summary>
	private StandardSubjectAssignment()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StandardSubjectAssignment"/> class with specified parameters.
	/// </summary>
	public StandardSubjectAssignment(
		Guid id,
		Guid? tenantId,
		Guid standardId,
		Guid subjectId,
		string subjectCode,
		SubjectStatus status = SubjectStatus.Draft,
		string? learningGoals = null,
		string? prerequisites = null,
		string? description = null,
		string? uploadedDocument = null
	)
		: base(id)
	{
		Check.NotNullOrEmpty(subjectCode, nameof(SubjectCode));
		TenantId = tenantId;
		StandardId = standardId;
		SubjectId = subjectId;
		SubjectCode = subjectCode;
		Status = status;

		LearningGoals = learningGoals;
		Prerequisites = prerequisites;
		Description = description;
		UploadedDocument = uploadedDocument;

		Teachers = new HashSet<SubjectTeacher>();
	}

	#endregion

	#region Methods
	public void ChangeStatus(SubjectStatus status)
	{
		Status = status;
	}

	public void SetPrimaryTeacher(Guid teacherId)
	{
		// find the current primary teacher
		var currentPrimary = Teachers.FirstOrDefault(t =>
			t.TeacherType == SubjectTeacherType.Primary
		);
		if (currentPrimary != null)
		{
			Teachers.Remove(currentPrimary);
		}
		Teachers.Add(new SubjectTeacher(Id, SubjectId, teacherId, SubjectTeacherType.Primary));
	}

	public void SetSecondaryTeachers(IEnumerable<Guid> teacherIds)
	{
		// find the current secondary teachers
		var currentSecondary = Teachers
			.Where(t => t.TeacherType == SubjectTeacherType.Secondary)
			.ToList();
		foreach (var teacher in currentSecondary)
		{
			Teachers.Remove(teacher);
		}
		foreach (var teacherId in teacherIds)
		{
			Teachers.Add(
				new SubjectTeacher(Id, SubjectId, teacherId, SubjectTeacherType.Secondary)
			);
		}
	}
	#endregion
}
