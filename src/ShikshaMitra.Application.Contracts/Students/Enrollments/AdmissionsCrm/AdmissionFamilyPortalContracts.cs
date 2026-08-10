using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class StartAdmissionFamilyAccessDto
{
	[Required]
	[MaxLength(256)]
	public string Contact { get; set; } = string.Empty;
}

public class VerifyAdmissionFamilyAccessDto
{
	public Guid ChallengeId { get; set; }

	[Required]
	[MaxLength(12)]
	public string OtpCode { get; set; } = string.Empty;

	[MaxLength(10)]
	public string? Language { get; set; }
}
