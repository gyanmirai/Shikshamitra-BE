using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentContactDetails(
	string? name,
	DateTimeOffset? dateOfBirth,
	string? occupation,
	string? qualification,
	string? contactNo,
	string? email,
	int? passOutYear,
	string? officeNo,
	string? officeAddress,
	string? designation,
	string? organisation,
	string? aadhaarNo,
	string? remarks,
	string? address
	) : ValueObject
{
	public string? Name { get; private set; } = name;
	public DateTimeOffset? DateOfBirth { get; private set; } = dateOfBirth;
	public string? Occupation { get; private set; } = occupation;
	public string? Qualification { get; private set; } = qualification;
	public string? ContactNo { get; private set; } = contactNo;
	public string? Email { get; private set; } = email;
	public int? PassOutYear { get; private set; } = passOutYear;
	public string? OfficeNo { get; private set; } = officeNo;
	public string? OfficeAddress { get; private set; } = officeAddress;
	public string? Designation { get; private set; } = designation;
	public string? Organisation { get; private set; } = organisation;
	public string? AadhaarNo { get; private set; } = aadhaarNo;
	public string? Remarks { get; private set; } = remarks;
	public string? Address { get; private set; } = address;
	
	protected override IEnumerable<object> GetAtomicValues()
	{
	yield return Name;
	yield return DateOfBirth;
	yield return Occupation;
	yield return Qualification;
	yield return ContactNo;
	yield return Email;
	yield return PassOutYear;
	yield return OfficeNo;
	yield return OfficeAddress;
	yield return Designation;
	yield return Organisation;
	yield return AadhaarNo;
	yield return Remarks;
	yield return Address;
	}
}