using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class AssignedSubjectTeacher : Entity, IMultiTenant
{
	#region Properties
	public virtual Guid? TenantId { get; private set; }

	/// <summary>
	/// The unique identifier for the subject associated with the teacher.
	/// </summary>
	public Guid AssignedSubjectId { get; private set; }

	/// <summary>
	/// The unique identifier for the teacher assigned to the subject.
	/// </summary>
	public Guid TeacherId { get; private set; }

	/// <summary>
	/// The type of teacher (e.g., primary, secondary) assigned to the subject.
	/// </summary>
	public SubjectTeacherType TeacherType { get; set; }
	#endregion

	#region Constructors
	private AssignedSubjectTeacher() { }

	public AssignedSubjectTeacher(Guid assignedSubjectId, Guid subjectId, Guid teacherId, SubjectTeacherType teacherType, Guid? tenantId = null)
	{
		AssignedSubjectId = assignedSubjectId;
		TeacherId = teacherId;
		TeacherType = teacherType;
		TenantId = tenantId;
	}

	public override object?[] GetKeys() => new object?[] { AssignedSubjectId, TeacherId };
	#endregion
}
