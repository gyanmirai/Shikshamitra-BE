using ShikshaMitra.HR.Employees;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectTeacher : Entity
{
	#region Properties
	public virtual Guid? TenantId { get; private set; }

    /// <summary>
    /// The unique identifier for the subject associated with the teacher.
    /// </summary>
    public Guid SubjectId { get; private set; }

    /// <summary>
    /// Navigation property to the Subject entity.
    /// </summary>
    [ForeignKey(nameof(SubjectId))]
	public StandardSubjectAssignment Subject { get; private set; }
	
	/// <summary>
    /// The unique identifier for the teacher assigned to the subject.
    /// </summary>
    public Guid TeacherId { get; private set; }
	
	/// <summary>
    /// Navigation property to the Employee entity representing the teacher.
    /// </summary>
    [ForeignKey(nameof(TeacherId))]
	public Employee Teacher { get; private set; }

	/// <summary>
	/// The type of teacher (e.g., primary, secondary) assigned to the subject.
	/// </summary>
	public SubjectTeacherType TeacherType { get; set; }
   	#endregion

	#region Constructors
	private SubjectTeacher() { }

	public SubjectTeacher(Guid id, Guid subjectId, Guid teacherId, SubjectTeacherType teacherType)
	{
		SubjectId = subjectId;
		TeacherId = teacherId;
		TeacherType = teacherType;
	}

	public override object?[] GetKeys()
	{
		return new object?[] { SubjectId, TeacherId };

	}
	#endregion
}
