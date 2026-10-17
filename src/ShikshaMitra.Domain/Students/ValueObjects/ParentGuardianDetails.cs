using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.ValueObjects;

/// <summary>
/// Value object representing parent or guardian details with contact and address information
/// </summary>
public class ParentGuardianDetails : ValueObject
{
	public string FullName { get; private set; }
	public DateTimeOffset? DateOfBirth { get; private set; }
	public string? Occupation { get; private set; }
	public string? Email { get; private set; }
	public string? ContactNo { get; private set; }
	public string? OfficeNo { get; private set; }
	public string? Qualification { get; private set; }
	public string? YearOfPassing { get; private set; }
	public string? Designation { get; private set; }
	public string? Organization { get; private set; }
	public string? AadharNo { get; private set; }

	// Office Address
	public AddressDetails? OfficeAddress { get; private set; }

	// Local Address
	public AddressDetails? LocalAddress { get; private set; }

	private ParentGuardianDetails()
	{
		/* For ORM/Serialization */
	}

	public ParentGuardianDetails(
		string? fullName,
		DateTimeOffset? dateOfBirth,
		string? occupation,
		string? email,
		string? contactNo,
		string? officeNo,
		string? qualification,
		string? yearOfPassing,
		string? designation,
		string? organization,
		string? aadharNo,
		AddressDetails? officeAddress,
		AddressDetails? localAddress)
	{
		// Keep an email/phone-only owned profile materializable when no name was bound.
		FullName = fullName ?? string.Empty;
		DateOfBirth = dateOfBirth;
		Occupation = occupation;
		Email = email;
		ContactNo = contactNo;
		OfficeNo = officeNo;
		Qualification = qualification;
		YearOfPassing = yearOfPassing;
		Designation = designation;
		Organization = organization;
		AadharNo = aadharNo;
		OfficeAddress = officeAddress;
		LocalAddress = localAddress;
	}

	protected override IEnumerable<object?> GetAtomicValues()
	{
		yield return FullName;
		yield return DateOfBirth;
		yield return Occupation;
		yield return Email;
		yield return ContactNo;
		yield return OfficeNo;
		yield return Qualification;
		yield return YearOfPassing;
		yield return Designation;
		yield return Organization;
		yield return AadharNo;
		yield return OfficeAddress;
		yield return LocalAddress;
	}
}
