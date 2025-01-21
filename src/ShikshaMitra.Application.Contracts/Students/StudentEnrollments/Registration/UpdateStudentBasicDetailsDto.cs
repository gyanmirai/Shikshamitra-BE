using ShikshaMitra.Common;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class UpdateStudentBasicDetailsDto
{
	public Guid? UserId { get; set; }
	public byte[]? ProfilePicture { get; set; }
	public string? AadhaarNo { get; set; }
	public string? Religion { get; set; }
	public string? FamilyCast { get; set; }
	public CastType? CastType { get; set; }
	public string? MotherTongue { get; set; }
	public string? Nationality { get; set; }
	public string? BirthPlace { get; set; }
	public string? TemporaryGr { get; set; }
	public string? Remarks { get; set; }
}
