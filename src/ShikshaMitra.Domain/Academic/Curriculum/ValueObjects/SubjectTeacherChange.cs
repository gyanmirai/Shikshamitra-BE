using System;
using System.Linq;

namespace ShikshaMitra.Academic.Curriculum;

public sealed record SubjectTeacherChange(AssignedSubject Assignment, Guid? PreviousPrimaryTeacherId)
{
	public Guid? PrimaryTeacherId => Assignment.Teachers
		.FirstOrDefault(teacher => teacher.TeacherType == SubjectTeacherType.Primary)?.TeacherId;

	public bool Replaces(Guid? teacherId) => teacherId.HasValue && PrimaryTeacherId.HasValue &&
		teacherId != PrimaryTeacherId &&
		(teacherId == PreviousPrimaryTeacherId || !Assignment.Teachers.Any(teacher => teacher.TeacherId == teacherId));
}
