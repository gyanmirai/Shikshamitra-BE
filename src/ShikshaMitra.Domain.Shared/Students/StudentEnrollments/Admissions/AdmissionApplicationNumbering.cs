namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public enum AdmissionNumberYearFormat { None = 0, CalendarYear = 1, AcademicYear = 2 }
public enum AdmissionNumberResetPolicy { Continuous = 0, CalendarYear = 1, AcademicYear = 2 }

public static class AdmissionNumberingErrorCodes
{
	public const string Invalid = "Students.Admissions.Numbering.Invalid";
	public const string Changed = "Students.Admissions.Numbering.Changed";
	public const string SequenceLocked = "Students.Admissions.Numbering.SequenceLocked";
}
