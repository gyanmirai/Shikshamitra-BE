using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students;

public class StudentDto : EntityDto<Guid>
{
	public Guid UserId { get; private set; }
	public string GrNo { get; set; }
	public string Surname { get; set; }
	public string Name { get; set; }
	public string MiddleName { get; set; }
	public DateOnly DateOfBirth { get; set; }
	public string Gender { get; set; }
	public Guid StandardId { get;  set; }
	public Guid DivisionId { get;  set; }
	public AddressDto Address { get; set; }
	public StudentParentDetailsDto FatherDetails { get; set; }
	public StudentParentDetailsDto MotherDetails { get; set; }
	public byte[]? ProfilePicture { get; set; }
}