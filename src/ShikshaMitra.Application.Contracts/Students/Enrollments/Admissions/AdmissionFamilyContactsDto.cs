using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Child-specific contacts; these are never proof of a shared portal identity.</summary>
public class AdmissionFamilyContactsDto
{
	public AdmissionFamilyContactDto? OtherParent { get; set; }
	public AdmissionFamilyContactDto? OtherGuardian { get; set; }
	[RegularExpression("^(father|mother|guardian)$")]
	public string? MainContactRelationship { get; set; }
}

public class AdmissionFamilyContactDto
{
	[Required, StringLength(256)]
	public string FullName { get; set; } = string.Empty;
	[Required, StringLength(128)]
	public string Relationship { get; set; } = string.Empty;
	[StringLength(32)]
	public string? Mobile { get; set; }
	[EmailAddress, StringLength(256)]
	public string? Email { get; set; }
}

public class AdmissionSchoolContactDto
{
	public string FullName { get; set; } = string.Empty;
	public string Relationship { get; set; } = string.Empty;
	public string? RelationshipLabel { get; set; }
	public string? Mobile { get; set; }
	public string? Email { get; set; }
}
