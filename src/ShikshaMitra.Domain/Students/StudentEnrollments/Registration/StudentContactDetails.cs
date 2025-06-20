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
	string? officeAddress1,
	string? officeAddress2,
	string? officeAddress3,
	string? designation,
	string? organisation,
	string? aadhaarNo,
	string? remarks,
	string? address1,
	string? address2,
	string? address3
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
	public string? OfficeAddress1 { get; private set; } = officeAddress1;
	public string? OfficeAddress2 { get; private set; } = officeAddress2;
	public string? OfficeAddress3 { get; private set; } = officeAddress3;
	public string? Designation { get; private set; } = designation;
	public string? Organisation { get; private set; } = organisation;
	public string? AadhaarNo { get; private set; } = aadhaarNo;
	public string? Remarks { get; private set; } = remarks;
	public string? Address1 { get; private set; } = address1;
	public string? Address2 { get; private set; } = address2;
	public string? Address3 { get; private set; } = address3;


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
		yield return OfficeAddress1;
		yield return OfficeAddress2;
		yield return OfficeAddress3;
		yield return Designation;
		yield return Organisation;
		yield return AadhaarNo;
		yield return Remarks;
		yield return Address1;
		yield return Address2;
		yield return Address3;
	}
}