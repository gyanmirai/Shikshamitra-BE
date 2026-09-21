namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public enum AdmissionReferenceKind
{
	Inquiry = 0,
	Application = 1,
	Family = 2
}

public static class AdmissionReferenceNumber
{
	public static string Format(AdmissionReferenceKind kind, int year, long sequence)
	{
		var prefix = kind switch
		{
			AdmissionReferenceKind.Inquiry => "INQ",
			AdmissionReferenceKind.Application => "APP",
			AdmissionReferenceKind.Family => "FAM",
			_ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null)
		};

		return $"{prefix}-{year}-{sequence:D6}";
	}
}
