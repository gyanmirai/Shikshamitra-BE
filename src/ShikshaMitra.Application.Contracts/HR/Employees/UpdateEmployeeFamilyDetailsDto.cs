using System;

namespace ShikshaMitra.HR.Employees;

public class UpdateEmployeeFamilyDetailsDto
{
	public ContactDetailsDto FatherDetails { get; set; }
	public ContactDetailsDto MotherDetails { get; set; }
	public ContactDetailsDto SpouseDetails { get; set; }
}