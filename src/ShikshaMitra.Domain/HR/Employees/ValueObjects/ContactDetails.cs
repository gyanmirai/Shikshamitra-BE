using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.HR.Employees;

public class ContactDetails(
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
	: ValueObject
{
	public string? Name { get; private set; } = name;
	public string? Occupation { get; private set; } = occupation;
	public string? ContactNo { get; private set; } = contactNo;
	public string? Address1 { get; private set; } = address1;
	public string? Address2 { get; private set; } = address2;
	public string? Address3 { get; private set; } = address3;
	public string? City { get; private set; } = city;
	public string? StateName { get; private set; } = stateName;
	public string? StateIsoCode { get; private set; } = stateIsoCode;
	public string? CountryName { get; private set; } = countryName;
	public string? CountryIsoCode { get; private set; } = countryIsoCode;
	public string? PinCode { get; private set; } = pinCode;

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Name ?? string.Empty;
		yield return Occupation ?? string.Empty;
		yield return ContactNo ?? string.Empty;
		yield return Address1 ?? string.Empty;
		yield return Address2 ?? string.Empty;
		yield return Address3 ?? string.Empty;
		yield return City ?? string.Empty;
		yield return StateName ?? string.Empty;
		yield return StateIsoCode ?? string.Empty;
		yield return CountryName ?? string.Empty;
		yield return CountryIsoCode ?? string.Empty;
		yield return PinCode ?? string.Empty;
	}
}
