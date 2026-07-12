using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class RetryAdmissionCrmMessageDeliveryDto
{
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? Reason { get; set; }
}
