using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Represents a family member of an employee (Father, Mother, or Spouse)
/// </summary>
public class EmployeeFamilyMember : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// The employee this family member belongs to
	/// </summary>
	public Guid EmployeeId { get; private set; }

	/// <summary>
	/// Type of relationship (Father, Mother, Spouse)
	/// </summary>
	public FamilyRelationType RelationType { get; private set; }

	/// <summary>
	/// Name of the family member
	/// </summary>
	public string? Name { get; private set; }

	/// <summary>
	/// Occupation of the family member
	/// </summary>
	public string? Occupation { get; private set; }

	/// <summary>
	/// Contact number
	/// </summary>
	public string? ContactNo { get; private set; }

	/// <summary>
	/// Address line 1
	/// </summary>
	public string? Address1 { get; private set; }

	/// <summary>
	/// Address line 2
	/// </summary>
	public string? Address2 { get; private set; }

	/// <summary>
	/// Address line 3
	/// </summary>
	public string? Address3 { get; private set; }

	/// <summary>
	/// City
	/// </summary>
	public string? City { get; private set; }

	/// <summary>
	/// State name
	/// </summary>
	public string? StateName { get; private set; }

	/// <summary>
	/// State ISO code
	/// </summary>
	public string? StateIsoCode { get; private set; }

	/// <summary>
	/// Country name
	/// </summary>
	public string? CountryName { get; private set; }

	/// <summary>
	/// Country ISO code
	/// </summary>
	public string? CountryIsoCode { get; private set; }

	/// <summary>
	/// Postal/Pin code
	/// </summary>
	public string? PinCode { get; private set; }

	/// <summary>
	/// Navigation property to Employee
	/// </summary>
	public virtual Employee? Employee { get; private set; }

	private EmployeeFamilyMember() { } // For ORM

	public EmployeeFamilyMember(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		FamilyRelationType relationType,
		string? name,
		string? occupation,
		string? contactNo,
		string? address1,
		string? address2,
		string? address3,
		string? city,
		string? stateName,
		string? stateIsoCode,
		string? countryName,
		string? countryIsoCode,
		string? pinCode) : base(id)
	{
		TenantId = tenantId;
		EmployeeId = employeeId;
		RelationType = relationType;
		Name = name;
		Occupation = occupation;
		ContactNo = contactNo;
		Address1 = address1;
		Address2 = address2;
		Address3 = address3;
		City = city;
		StateName = stateName;
		StateIsoCode = stateIsoCode;
		CountryName = countryName;
		CountryIsoCode = countryIsoCode;
		PinCode = pinCode;
	}

	public void Update(
		string? name,
		string? occupation,
		string? contactNo,
		string? address1,
		string? address2,
		string? address3,
		string? city,
		string? stateName,
		string? stateIsoCode,
		string? countryName,
		string? countryIsoCode,
		string? pinCode)
	{
		Name = name;
		Occupation = occupation;
		ContactNo = contactNo;
		Address1 = address1;
		Address2 = address2;
		Address3 = address3;
		City = city;
		StateName = stateName;
		StateIsoCode = stateIsoCode;
		CountryName = countryName;
		CountryIsoCode = countryIsoCode;
		PinCode = pinCode;
	}
}
