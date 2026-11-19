using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Recognizes persistence conflicts without exposing provider-specific types to application services.</summary>
public interface IAdmissionFinalizationConflictClassifier
{
	bool IsStudentIdentityConflict(Exception exception);
	bool IsConcurrentFinalization(Exception exception);
}
