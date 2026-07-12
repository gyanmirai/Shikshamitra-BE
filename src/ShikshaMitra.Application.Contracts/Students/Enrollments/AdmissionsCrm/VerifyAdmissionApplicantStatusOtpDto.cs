using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class VerifyAdmissionApplicantStatusOtpDto
{
	[Required]
	[MaxLength(12)]
	public required string OtpCode { get; set; }

	[MaxLength(10)]
	public string? Language { get; set; }
}
