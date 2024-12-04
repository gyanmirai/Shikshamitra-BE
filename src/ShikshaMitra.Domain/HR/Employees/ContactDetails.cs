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
	string? state,
	string? pinCode)
	: ValueObject
{
	public string? Name { get; private set; } = name;
	public string? Occupation { get; private set; } = occupation;
	public string? ContactNo { get; private set; } = contactNo;
	public string? Address { get; private set; } = address;
	public string? City { get; private set; } = city;
	public string? State { get; private set; } = state;
	public string? PinCode { get; private set; } = pinCode;

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Name;
		yield return Occupation;
		yield return ContactNo;
		yield return Address;
		yield return City;
		yield return State;
		yield return PinCode;
	}
}