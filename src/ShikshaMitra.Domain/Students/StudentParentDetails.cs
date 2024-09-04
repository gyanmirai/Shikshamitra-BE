using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students;

public class StudentParentDetails : ValueObject
{
	public string? Name { get; private set; }
	public DateOnly? DateOfBirth { get; private set; }
	public string? Occupation { get; private set; }
	public string? Qualification { get; private set; }
	public string? ExStudent { get; private set; }
	public int? PassOutYear { get; private set; }
	public string? Organization { get; private set; }
	public string? Designation { get; private set; }
	public string? OfficeAddress { get; private set; }
	public string? ContactNumber { get; private set; }
	public string? OfficeContactNumber { get; private set; }
	public string? Email { get; private set; }
	public string? AadharNo { get; private set; }
	public decimal? AnnualIncome { get; private set; }

	// Constructor for ORM
	private StudentParentDetails() { }

	internal StudentParentDetails(
		string name,
		DateOnly dateOfBirth,
		string occupation,
		string qualification,
		string exStudent,
		int passOutYear,
		string organization,
		string designation,
		string officeAddress,
		string contactNumber,
		string officeContactNumber,
		string email,
		string aadharNo,
		decimal annualIncome)
	{
		Name = name;
		DateOfBirth = dateOfBirth;
		Occupation = occupation;
		Qualification = qualification;
		ExStudent = exStudent;
		PassOutYear = passOutYear;
		Organization = organization;
		Designation = designation;
		OfficeAddress = officeAddress;
		ContactNumber = contactNumber;
		OfficeContactNumber = officeContactNumber;
		Email = email;
		AadharNo = aadharNo;
		AnnualIncome = annualIncome;
	}

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Name;
		yield return DateOfBirth;
		yield return Occupation;
		yield return Qualification;
		yield return ExStudent;
		yield return PassOutYear;
		yield return Organization;
		yield return Designation;
		yield return OfficeAddress;
		yield return ContactNumber;
		yield return OfficeContactNumber;
		yield return Email;
		yield return AadharNo;
		yield return AnnualIncome;
	}
}