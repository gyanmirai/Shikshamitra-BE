using System;

namespace ShikshaMitra.Students;

public class CreateUpdateStudentDto
{
	public string GrNo { get; set; }
	public string Surname { get; set; }
	public string Name { get; set; }
	public string MiddleName { get; set; }
	public DateTime? DateOfBirth { get; set; }
	public string Gender { get; set; }
	public Guid StandardId { get;  set; }
	public Guid DivisionId { get;  set; }
	public AddressDto Address { get; set; }
	// public AdmissionDetailsDto AdmissionDetails { get; set; }
	// public CommunicationDetailsDto CommunicationDetails { get; set; }
	public ParentDetailsDto FatherDetails { get; set; }
	public ParentDetailsDto MotherDetails { get; set; }
	public byte[] ProfilePicture { get; set; }
}