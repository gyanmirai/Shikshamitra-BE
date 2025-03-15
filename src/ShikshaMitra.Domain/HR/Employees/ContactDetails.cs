using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.HR.Employees;

public class ContactDetails(
	string? name,
	string? occupation,
	string? contactNo,
	string? address,
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
	public string? Address { get; private set; } = address;
	public string? City { get; private set; } = city;
	public string? StateName { get; private set; } = stateName;
	public string? StateIsoCode { get; private set; } = stateIsoCode;
	public string? CountryName { get; private set; } = countryName;
	public string? CountryIsoCode { get; private set; } = countryIsoCode;
	public string? PinCode { get; private set; } = pinCode;

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Name;
		yield return Occupation;
		yield return ContactNo;
		yield return Address;
		yield return City;
		yield return StateName;
		yield return StateIsoCode;
		yield return CountryName;
		yield return CountryIsoCode;
		yield return PinCode;
	}
}