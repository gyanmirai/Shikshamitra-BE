using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.Students;

public class CreateUpdateStudentDto
{
	public Guid UserId { get; set; }
	public string GrNo { get; set; }
	public string Surname { get; set; }
	public string Name { get; set; }
	public string MiddleName { get; set; }
	public DateOnly? DateOfBirth { get; set; }
	public GenderType GenderType { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public AddressDto Address { get; set; }
	public AdmissionDetailsDto AdmissionDetails { get; set; }
	public CommunicationDetailsDto CommunicationDetails { get; set; }
	public StudentParentDetailsDto FatherDetails { get; set; }
	public StudentParentDetailsDto MotherDetails { get; set; }
	public byte[]? ProfilePicture { get; set; }
}